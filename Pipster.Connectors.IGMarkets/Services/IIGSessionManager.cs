using Pipster.Connectors.IGMarkets.Models.Authentication;

namespace Pipster.Connectors.IGMarkets.Services;

/// <summary>
/// Manages IG session lifecycle including authentication and token refresh
/// </summary>
public interface IIGSessionManager
{
    /// <summary>
    /// Gets a valid session token, creating a new session if needed
    /// </summary>
    Task<IGSessionTokens> GetValidSessionAsync(CancellationToken ct = default);

    /// <summary>
    /// Forces a session refresh
    /// </summary>
    Task RefreshSessionAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears the current session
    /// </summary>
    void ClearSession();
}