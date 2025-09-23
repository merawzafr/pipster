using Pipster.Shared.Contracts;

public interface ISignalParser
{
    NormalizedSignal? TryParse(string tenantId, string source, string messageText);
}