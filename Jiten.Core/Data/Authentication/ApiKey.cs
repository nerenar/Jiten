using Jiten.Core.Data.Authentication;

public class ApiKey
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Hash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsRevoked { get; set; }

    public User User { get; set; } = null!;

    public bool IsValid => !IsRevoked && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}