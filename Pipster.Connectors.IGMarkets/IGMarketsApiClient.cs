using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pipster.Connectors.IGMarkets.Configuration;
using Pipster.Connectors.IGMarkets.Models.Authentication;
using Pipster.Connectors.IGMarkets.Models.Common;
using Pipster.Connectors.IGMarkets.Models.Positions;
using Pipster.Connectors.IGMarkets.Services;

namespace Pipster.Connectors.IGMarkets;

/// <summary>
/// Interface for IG Markets API client
/// </summary>
public interface IIGMarketsApiClient
{
    /// <summary>
    /// Creates a new position (places a trade)
    /// </summary>
    Task<IGDealReference> CreatePositionAsync(
        IGCreatePositionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets deal confirmation status
    /// </summary>
    Task<IGDealConfirmation> GetDealConfirmationAsync(
        string dealReference,
        CancellationToken ct = default);

    /// <summary>
    /// Gets market details for an epic
    /// </summary>
    Task<IGMarketDetails> GetMarketDetailsAsync(
        string epic,
        CancellationToken ct = default);
}

/// <summary>
/// HTTP client for IG Markets REST API
/// </summary>
public sealed class IGMarketsApiClient : IIGMarketsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IIGSessionManager _sessionManager;
    private readonly IGMarketsOptions _options;
    private readonly ILogger<IGMarketsApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IGMarketsApiClient(
        HttpClient httpClient,
        IIGSessionManager sessionManager,
        IOptions<IGMarketsOptions> options,
        ILogger<IGMarketsApiClient> logger)
    {
        _httpClient = httpClient;
        _sessionManager = sessionManager;
        _options = options.Value;
        _logger = logger;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<IGDealReference> CreatePositionAsync(
        IGCreatePositionRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating {Direction} position for {Epic}, size: {Size}, orderType: {OrderType}",
            request.Direction,
            request.Epic,
            request.Size,
            request.OrderType);

        var response = await SendAuthenticatedRequestAsync<IGDealReference>(
            HttpMethod.Post,
            "/positions/otc",
            request,
            version: "2",
            ct);

        _logger.LogInformation(
            "Position creation initiated. Deal reference: {DealReference}",
            response.DealReference);

        return response;
    }

    public async Task<IGDealConfirmation> GetDealConfirmationAsync(
        string dealReference,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting deal confirmation for {DealReference}", dealReference);

        var response = await SendAuthenticatedRequestAsync<IGDealConfirmation>(
            HttpMethod.Get,
            $"/confirms/{dealReference}",
            requestBody: null,
            version: "1",
            ct);

        _logger.LogInformation(
            "Deal {DealReference} status: {Status}, dealId: {DealId}",
            dealReference,
            response.DealStatus,
            response.DealId);

        return response;
    }

    public async Task<IGMarketDetails> GetMarketDetailsAsync(
        string epic,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting market details for {Epic}", epic);

        var response = await SendAuthenticatedRequestAsync<IGMarketDetails>(
            HttpMethod.Get,
            $"/markets/{epic}",
            requestBody: null,
            version: "3",
            ct);

        _logger.LogDebug(
            "Market {Epic}: {Name}, status: {Status}",
            epic,
            response.Instrument?.Name,
            response.Snapshot?.MarketStatus);

        return response;
    }

    private async Task<TResponse> SendAuthenticatedRequestAsync<TResponse>(
        HttpMethod method,
        string path,
        object? requestBody,
        string version,
        CancellationToken ct)
    {
        // Get valid session tokens
        var session = await _sessionManager.GetValidSessionAsync(ct);

        using var request = new HttpRequestMessage(method, path);

        // Add required IG headers
        request.Headers.Add("X-IG-API-KEY", _options.ApiKey);
        request.Headers.Add("CST", session.ClientSessionToken);
        request.Headers.Add("X-SECURITY-TOKEN", session.SecurityToken);
        request.Headers.Add("Version", version);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Add request body if provided
        if (requestBody != null)
        {
            request.Content = JsonContent.Create(requestBody, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling IG API: {Method} {Path}", method, path);
            throw new InvalidOperationException($"Failed to connect to IG API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout calling IG API: {Method} {Path}", method, path);
            throw new InvalidOperationException("IG API request timed out", ex);
        }

        // Handle response
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponseAsync(response, method, path, ct);
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);

            if (result == null)
            {
                throw new InvalidOperationException("IG API returned null response");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize IG API response");
            throw new InvalidOperationException("Failed to parse IG API response", ex);
        }
    }

    private async Task HandleErrorResponseAsync(
        HttpResponseMessage response,
        HttpMethod method,
        string path,
        CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        var errorContent = await response.Content.ReadAsStringAsync(ct);

        _logger.LogError(
            "IG API error: {Method} {Path} returned {StatusCode}. Response: {Error}",
            method,
            path,
            statusCode,
            errorContent);

        // Try to parse IG error response
        IGErrorResponse? igError = null;
        try
        {
            igError = JsonSerializer.Deserialize<IGErrorResponse>(errorContent, JsonOptions);
        }
        catch
        {
            // Ignore parse errors, we'll use raw content
        }

        var errorMessage = igError?.ErrorMessage ?? errorContent;
        var errorCode = igError?.ErrorCode ?? response.StatusCode.ToString();

        // Handle specific error codes
        Exception exception = statusCode switch
        {
            401 => new UnauthorizedAccessException(
                $"IG authentication failed. Check credentials. Code: {errorCode}, Message: {errorMessage}"),

            403 => new UnauthorizedAccessException(
                $"IG access forbidden. Check API permissions. Code: {errorCode}, Message: {errorMessage}"),

            404 => new InvalidOperationException(
                $"IG resource not found: {path}. Message: {errorMessage}"),

            429 => new InvalidOperationException(
                $"IG rate limit exceeded. Code: {errorCode}, Message: {errorMessage}"),

            >= 500 => new InvalidOperationException(
                $"IG server error. Code: {errorCode}, Message: {errorMessage}"),

            _ => new InvalidOperationException(
                $"IG API error {statusCode}. Code: {errorCode}, Message: {errorMessage}")
        };

        throw exception;
    }
}