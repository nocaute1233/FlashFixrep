namespace FlashFix.Api.Data;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string NormalizedUsername { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public string? AdminDeviceHash { get; set; }
    public bool IsBlocked { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public sealed class LicenseKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Prefix { get; set; }
    public required string KeyHash { get; set; }
    public int? DurationDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsBlocked { get; set; }
    public Guid? AssignedUserId { get; set; }
    public UserAccount? AssignedUser { get; set; }
    public string? DeviceHash { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class SessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    public required string AccessHash { get; set; }
    public required string RefreshHash { get; set; }
    public required string DeviceHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime AccessExpiresAt { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? LicenseKeyId { get; set; }
    public required string Action { get; set; }
    public required string Outcome { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Detail { get; set; }
}

