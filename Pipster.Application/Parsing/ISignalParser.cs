using Pipster.Shared.Contracts;
namespace Pipster.Application.Parsing;

public interface ISignalParser
{
    NormalizedSignal? TryParse(string regex, string signal);
}