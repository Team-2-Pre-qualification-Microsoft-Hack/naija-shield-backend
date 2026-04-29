namespace naija_shield_backend.DTOs;

public class InviteResponse
{
    public string InviteId { get; set; } = string.Empty;
    public string InviteToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "Pending";
    public bool EmailSent { get; set; }
}
