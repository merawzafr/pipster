using Microsoft.Extensions.Logging;
using Pipster.Shared.Contracts;

namespace Pipster.Connectors.IGMarkets;

/// <summary>
/// Provider for creating IG Markets connector instances.
/// Registered in DI to allow factory to create connectors without direct dependency.
/// </summary>
public sealed class IGMarketsConnectorProvider : ITradeConnectorProvider
{
    private readonly IIGMarketsApiClient _apiClient;
    private readonly ILogger<IGMarketsConnector> _logger;

    public string BrokerType => "IGMarkets";

    public IGMarketsConnectorProvider(
        IIGMarketsApiClient apiClient,
        ILogger<IGMarketsConnector> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public ITradeConnector CreateConnector()
    {
        return new IGMarketsConnector(_apiClient, _logger);
    }
}