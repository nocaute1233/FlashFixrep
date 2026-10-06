using FlashFix.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FlashFix.Api.Security;

public sealed record AuthContext(UserAccount User, SessionRecord Session, LicenseKey? License);

public static class SessionAuth
{
    public static async Task<AuthContext?> ResolveAsync(HttpContext http, FlashFixDb db)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var raw = authorization[7..].Trim();
        if (raw.Length is < 30 or > 128)
            return null;

        var hash = SecretCodec.HashToken(raw);
        var now = DateTime.UtcNow;
        var session = await db.Sessions.Include(x => x.User).FirstOrDefaultAsync(x =>
            x.AccessHash == hash && x.RevokedAt == null && x.AccessExpiresAt > now);
        if (session is null || session.User.IsBlocked)
            return null;

        if (session.User.IsAdmin)
            return new AuthContext(session.User, session, null);

        var license = await db.LicenseKeys.FirstOrDefaultAsync(x => x.AssignedUserId == session.UserId);
        if (license is null || !IsValid(license, session.DeviceHash, now))
            return null;

        return new AuthContext(session.User, session, license);
    }

    public static bool IsValid(LicenseKey license, string deviceHash, DateTime now) =>
        license.AssignedUserId is not null && license.RevokedAt is null &&
        !license.IsBlocked && license.DeviceHash == deviceHash &&
        (license.ExpiresAt is null || license.ExpiresAt > now);

    public static (SessionRecord Session, SessionTokens Tokens) NewSession(
        UserAccount user, string deviceHash)
    {
        var access = SecretCodec.NewToken();
        var refresh = SecretCodec.NewToken();
        var now = DateTime.UtcNow;
        var record = new SessionRecord
        {
            UserId = user.Id,
            User = user,
            DeviceHash = deviceHash,
            AccessHash = SecretCodec.HashToken(access),
            RefreshHash = SecretCodec.HashToken(refresh),
            AccessExpiresAt = now.AddMinutes(user.IsAdmin ? 5 : 15),
            RefreshExpiresAt = user.IsAdmin ? now.AddHours(8) : now.AddDays(30)
        };
        return (record, new SessionTokens(access, refresh, record.AccessExpiresAt));
    }
}

public sealed record SessionTokens(string AccessToken, string RefreshToken, DateTime AccessExpiresAt);

