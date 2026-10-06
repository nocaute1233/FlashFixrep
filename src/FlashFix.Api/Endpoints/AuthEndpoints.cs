using FlashFix.Api.Data;
using FlashFix.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace FlashFix.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly string UnusedPasswordHash = BCrypt.Net.BCrypt.HashPassword(
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)), workFactor: 12);

    public sealed record ChallengeRequest(string Username, string Purpose);
    public sealed record RegisterRequest(string Username, string Password, string Key, string DeviceId,
        string? ChallengeId, string? DevicePublicKey, string? DeviceSignature);
    public sealed record LoginRequest(string Username, string Password, string DeviceId,
        string? ChallengeId, string? DevicePublicKey, string? DeviceSignature);
    public sealed record RefreshRequest(string RefreshToken, string DeviceId,
        string? ChallengeId, string? DevicePublicKey, string? DeviceSignature);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/auth").RequireRateLimiting("auth");
        group.MapPost("/device-challenge", ChallengeAsync);
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", MeAsync);
    }

    private static IResult ChallengeAsync(ChallengeRequest request, DeviceChallenges challenges)
    {
        if (!InputRules.Username(request.Username) ||
            request.Purpose is not ("register" or "login" or "refresh"))
            return Results.BadRequest(new { error = "Solicitação inválida." });
        var challenge = challenges.Create(InputRules.NormalizeUsername(request.Username), request.Purpose);
        return challenge is null ? Results.StatusCode(503) :
            Results.Ok(new { challengeId = challenge.Value.Id, payload = challenge.Value.Payload });
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, FlashFixDb db, SecretCodec codec, DeviceChallenges challenges)
    {
        if (!InputRules.Username(request.Username) || !InputRules.Password(request.Password) ||
            !InputRules.Key(request.Key) || !InputRules.Device(request.DeviceId))
            return Results.BadRequest(new { error = "Dados de cadastro inválidos." });

        var normalized = InputRules.NormalizeUsername(request.Username);
        if (!challenges.Consume(request.ChallengeId, normalized, "register",
                request.DevicePublicKey, request.DeviceSignature, codec, out var deviceHash))
            return Results.BadRequest(new { error = "Confirmação do dispositivo inválida." });
        var keyHash = codec.HashKey(request.Key);
        var key = await db.LicenseKeys.SingleOrDefaultAsync(x => x.KeyHash == keyHash);
        if (key is null || key.AssignedUserId is not null || key.RevokedAt is not null || key.IsBlocked)
            return Results.BadRequest(new { error = "Chave indisponível." });

        if (await db.Users.AnyAsync(x => x.NormalizedUsername == normalized))
            return Results.Conflict(new { error = "Nome de usuário indisponível." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new UserAccount
        {
            Username = request.Username,
            NormalizedUsername = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12)
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Results.Conflict(new { error = "Nome de usuário indisponível." });
        }

        var now = DateTime.UtcNow;
        DateTime? expiry = key.DurationDays is int days ? now.AddDays(days) : null;
        var assigned = await db.LicenseKeys.Where(x => x.Id == key.Id && x.AssignedUserId == null &&
            x.RevokedAt == null && !x.IsBlocked).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.AssignedUserId, user.Id)
            .SetProperty(x => x.DeviceHash, deviceHash)
            .SetProperty(x => x.ActivatedAt, now)
            .SetProperty(x => x.ExpiresAt, expiry));
        if (assigned != 1)
        {
            await transaction.RollbackAsync();
            return Results.Conflict(new { error = "Chave já ativada." });
        }

        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = user.Id, LicenseKeyId = key.Id,
            Action = "license.activate", Outcome = "success"
        });
        var (session, tokens) = SessionAuth.NewSession(user, deviceHash);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(tokens);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, FlashFixDb db, SecretCodec codec, DeviceChallenges challenges)
    {
        if (!InputRules.Username(request.Username) || !InputRules.Password(request.Password) ||
            !InputRules.Device(request.DeviceId))
            return Results.BadRequest(new { error = "Dados de acesso inválidos." });

        var normalized = InputRules.NormalizeUsername(request.Username);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedUsername == normalized);
        var now = DateTime.UtcNow;
        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password,
            user?.PasswordHash ?? UnusedPasswordHash);
        if (user is null || user.IsBlocked || user.LockedUntil > now || !passwordValid)
        {
            if (user is not null && !user.IsBlocked &&
                (user.LockedUntil is null || user.LockedUntil <= now) && !passwordValid)
            {
                await db.Users.Where(x => x.Id == user.Id && !x.IsBlocked &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.FailedLogins, x => x.FailedLogins + 1));
                await db.Users.Where(x => x.Id == user.Id && x.FailedLogins >= 5 &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.LockedUntil, now.AddMinutes(15))
                        .SetProperty(x => x.FailedLogins, 0));
                db.AuditEvents.Add(new AuditEvent
                {
                    ActorUserId = user.Id, Action = "auth.login", Outcome = "failure"
                });
                await db.SaveChangesAsync();
            }
            return Results.Unauthorized();
        }

        var deviceHash = codec.HashDevice(request.DeviceId);
        LicenseKey? license = null;
        if (!user.IsAdmin)
        {
            if (!challenges.Consume(request.ChallengeId, normalized, "login",
                    request.DevicePublicKey, request.DeviceSignature, codec, out var provedDeviceHash))
                return Results.Json(new { error = "Confirmação do dispositivo inválida." }, statusCode: 403);
            deviceHash = provedDeviceHash;
            license = await db.LicenseKeys.SingleOrDefaultAsync(x => x.AssignedUserId == user.Id);
            if (license is null || license.RevokedAt is not null || license.IsBlocked ||
                (license.ExpiresAt is not null && license.ExpiresAt <= now))
                return Results.Json(new { error = "Licença expirada ou indisponível." }, statusCode: 403);

            if (license.DeviceHash is null)
                license.DeviceHash = deviceHash;
            else if (license.DeviceHash != deviceHash)
                return Results.Json(new { error = "Licença vinculada a outro dispositivo." }, statusCode: 403);

            license.LastLoginAt = now;
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        var (session, tokens) = SessionAuth.NewSession(user, deviceHash);
        db.Sessions.Add(session);
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = user.Id, LicenseKeyId = license?.Id,
            Action = "auth.login", Outcome = "success"
        });
        await db.SaveChangesAsync();
        return Results.Ok(tokens);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request, FlashFixDb db, SecretCodec codec, DeviceChallenges challenges)
    {
        if (request.RefreshToken is null || request.RefreshToken.Length is < 30 or > 128 ||
            !InputRules.Device(request.DeviceId))
            return Results.Unauthorized();

        var hash = SecretCodec.HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;
        var session = await db.Sessions.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x =>
            x.RefreshHash == hash && x.RevokedAt == null && x.RefreshExpiresAt > now);
        if (session is null || session.User.IsBlocked)
            return Results.Unauthorized();

        if (session.User.IsAdmin)
        {
            if (session.DeviceHash != codec.HashDevice(request.DeviceId)) return Results.Unauthorized();
        }
        else if (!challenges.Consume(request.ChallengeId, session.User.NormalizedUsername, "refresh",
                     request.DevicePublicKey, request.DeviceSignature, codec, out var provedDeviceHash) ||
                 session.DeviceHash != provedDeviceHash)
            return Results.Unauthorized();

        if (!session.User.IsAdmin)
        {
            var license = await db.LicenseKeys.SingleOrDefaultAsync(x => x.AssignedUserId == session.UserId);
            if (license is null || !SessionAuth.IsValid(license, session.DeviceHash, now))
                return Results.Unauthorized();
        }

        var access = SecretCodec.NewToken();
        var refresh = SecretCodec.NewToken();
        var accessHash = SecretCodec.HashToken(access);
        var refreshHash = SecretCodec.HashToken(refresh);
        var accessExpiresAt = now.AddMinutes(session.User.IsAdmin ? 5 : 15);
        var refreshExpiresAt = session.User.IsAdmin ? session.CreatedAt.AddHours(8) : now.AddDays(30);
        var updated = await db.Sessions.Where(x => x.Id == session.Id && x.RefreshHash == hash &&
            x.RevokedAt == null && x.RefreshExpiresAt > now).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.AccessHash, accessHash)
            .SetProperty(x => x.RefreshHash, refreshHash)
            .SetProperty(x => x.AccessExpiresAt, accessExpiresAt)
            .SetProperty(x => x.RefreshExpiresAt, refreshExpiresAt));
        if (updated != 1) return Results.Unauthorized();
        return Results.Ok(new SessionTokens(access, refresh, accessExpiresAt));
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, FlashFixDb db)
    {
        var auth = await SessionAuth.ResolveAsync(http, db);
        if (auth is null) return Results.Unauthorized();
        auth.Session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(HttpContext http, FlashFixDb db)
    {
        var auth = await SessionAuth.ResolveAsync(http, db);
        if (auth is null) return Results.Unauthorized();
        return Results.Ok(new
        {
            auth.User.Id,
            auth.User.Username,
            auth.User.IsAdmin,
            License = auth.License is null ? null : new
            {
                auth.License.Id,
                auth.License.ActivatedAt,
                auth.License.ExpiresAt,
                Status = "active"
            }
        });
    }
}

