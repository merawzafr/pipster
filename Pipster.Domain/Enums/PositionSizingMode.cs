namespace Pipster.Domain.Enums;

/// <summary>
/// Position sizing strategies
/// </summary>
public enum PositionSizingMode
{
    Fixed = 1,         // Fixed units per trade
    PercentEquity = 2  // Percentage of account equity
}
