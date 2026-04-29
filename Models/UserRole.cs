namespace naija_shield_backend.Models;

/// <summary>
/// User roles as defined in the auth spec. These exact strings are stored in the database and JWT.
/// </summary>
public enum UserRole
{
    SOC_ANALYST,
    COMPLIANCE_OFFICER,
    SYSTEM_ADMIN
}
