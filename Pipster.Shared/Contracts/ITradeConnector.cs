using Pipster.Shared.Contracts;

public interface ITradeConnector
{
    Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct);
}

public sealed class DummyConnector : ITradeConnector
{
    public Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct)
        => Task.FromResult($"SIM-{cmd.Symbol}-{Guid.NewGuid():N}");
}
