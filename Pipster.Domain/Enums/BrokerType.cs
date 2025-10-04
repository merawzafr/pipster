namespace Pipster.Domain.Enums;

/// <summary>
/// Supported broker types
/// </summary>
public enum BrokerType
{
    /// <summary>
    /// IG Markets (UK-based, EU-friendly)
    /// </summary>
    IGMarkets = 1,

    /// <summary>
    /// OANDA (US/Global, forex/metals)
    /// </summary>
    OANDA = 2,

    /// <summary>
    /// Interactive Brokers (global, all instruments)
    /// </summary>
    IBKR = 3,

    /// <summary>
    /// Alpaca (US stocks)
    /// </summary>
    Alpaca = 4,

    /// <summary>
    /// FOREX.com
    /// </summary>
    ForexCom = 5
}