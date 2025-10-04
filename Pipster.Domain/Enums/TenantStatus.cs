namespace Pipster.Domain.Enums;

/// <summary>
/// Tenant status
/// </summary>
public enum TenantStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3 // For billing issues, ToS violations, etc.
}
