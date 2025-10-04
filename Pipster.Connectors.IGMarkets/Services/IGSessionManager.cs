using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pipster.Connectors.IGMarkets.Configuration;
using Pipster.Connectors.IGMarkets.Models.Authentication;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pipster.Connectors.IGMarkets.Services;

/// <summary>
/// Manages IG session lifecycle with automatic token refresh
/// </summary>
public sealed class IGSessionManager : IIGSessionManager
{
    private readonly HttpClient _httpClient;
    private readonly IGMarketsOptions _options;
    private readonly ILogger<IGSessionManager> _logger;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    private IGSessionTokens? _currentSession;
    private Task<IGSessionTokens>? _refreshTask;

    public IGSessionManager(
        HttpClient httpClient,
        IOptions<IGMarketsOptions> options,
        ILogger<IGSessionManager> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure HttpClient base settings
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<IGSessionTokens> GetValidSessionAsync(CancellationToken ct = default)
    {
        // If we have a valid session, return it
        if (_currentSession != null && !_currentSession.IsExpired)
        {
            _logger.LogDebug("Returning cached session token");
            return _currentSession;
        }

        // If a refresh is already in progress, wait for it
        if (_refreshTask != null && !_refreshTask.IsCompleted)
        {
            _logger.LogDebug("Session refresh already in progress, waiting...");
            return await _refreshTask;
        }

        // Otherwise, create a new session
        await _sessionLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_currentSession != null && !_currentSession.IsExpired)
            {
                return _currentSession;
            }

            _logger.LogInformation("Creating new IG session");
            _refreshTask = CreateSessionInternalAsync(ct);
            _currentSession = await _refreshTask;
            return _currentSession;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task RefreshSessionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Forcing session refresh");

        await _sessionLock.WaitAsync(ct);
        try
        {
            _currentSession = await CreateSessionInternalAsync(ct);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public void ClearSession()
    {
        _logger.LogInformation("Clearing IG session");
        _currentSession = null;
        _refreshTask = null;
    }

    private async Task<IGSessionTokens> CreateSessionInternalAsync(CancellationToken ct)
    {
        var request = new IGSessionRequest
        {
            Identifier = _options.Username,
            Password = _options.Password
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/session");

        // IG requires specific headers
        httpRequest.Headers.Add("X-IG-API-KEY", _options.ApiKey);
        httpRequest.Headers.Add("Version", "2");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        httpRequest.Content = JsonContent.Create(request);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "IG session creation failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                throw new InvalidOperationException(
                    $"Failed to create IG session: {response.StatusCode}");
            }

            // Extract tokens from headers
            var cst = response.Headers.GetValues("CST").FirstOrDefault();
            var securityToken = response.Headers.GetValues("X-SECURITY-TOKEN").FirstOrDefault();

            if (string.IsNullOrEmpty(cst) || string.IsNullOrEmpty(securityToken))
            {
                _logger.LogError("IG session response missing required token headers");
                throw new InvalidOperationException("Missing session tokens in IG response");
            }

            // Parse response body for additional info
            var sessionResponse = await response.Content.ReadFromJsonAsync<IGSessionResponse>(ct);

            var tokens = new IGSessionTokens
            {
                ClientSessionToken = cst,
                SecurityToken = securityToken
            };

            _logger.LogInformation(
                "IG session created successfully. Account: {AccountId}, Expires: {Expiry}",
                sessionResponse?.AccountId ?? "unknown",
                tokens.ExpiresAt);

            return tokens;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error creating IG session");
            throw new InvalidOperationException("Failed to connect to IG API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating IG session");
            throw;
        }
    }
}