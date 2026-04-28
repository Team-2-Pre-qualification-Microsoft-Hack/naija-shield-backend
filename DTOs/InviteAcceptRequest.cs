namespace naija_shield_backend.DTOs;

public class InviteAcceptRequest
{
    public string InviteToken { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
