namespace Pipster.Domain.Entities;

/// <summary>
/// Trading session definition (UTC times)
/// </summary>
public sealed record TradingSession
{
    public TimeOnly StartUtc { get; init; }
    public TimeOnly EndUtc { get; init; }
    public DayOfWeek[]? AllowedDays { get; init; }

    public TradingSession(TimeOnly startUtc, TimeOnly endUtc, DayOfWeek[]? allowedDays = null)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
        AllowedDays = allowedDays;
    }

    public bool IsWithinSession(DateTimeOffset now)
    {
        var utcNow = now.UtcDateTime;
        var currentTime = TimeOnly.FromDateTime(utcNow);

        // Check day of week
        if (AllowedDays != null && AllowedDays.Length > 0)
        {
            if (!AllowedDays.Contains(utcNow.DayOfWeek))
                return false;
        }

        // Check time range
        if (StartUtc <= EndUtc)
        {
            // Normal range (e.g., 09:00 - 17:00)
            return currentTime >= StartUtc && currentTime <= EndUtc;
        }
        else
        {
            // Crosses midnight (e.g., 22:00 - 02:00)
            return currentTime >= StartUtc || currentTime <= EndUtc;
        }
    }
}