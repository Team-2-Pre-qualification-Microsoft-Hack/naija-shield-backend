namespace naija_shield_backend.Models;

public static class UserRole
{
    public const string SOC_ANALYST = "SOC_ANALYST";
    public const string COMPLIANCE_OFFICER = "COMPLIANCE_OFFICER";
    public const string SYSTEM_ADMIN = "SYSTEM_ADMIN";

    public static bool IsValid(string role)
    {
        return role == SOC_ANALYST || role == COMPLIANCE_OFFICER || role == SYSTEM_ADMIN;
    }
}
