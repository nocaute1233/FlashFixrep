using FlashFix.Api.Data;
using FlashFix.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace FlashFix.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record CreateKeyRequest(int? DurationDays);
    public sealed record ActionRequest(string Reason);

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/admin").RequireRateLimiting("admin");
        group.MapPost("/keys", CreateKeyAsync);
        group.MapGet("/keys", SearchKeysAsync);
        group.MapGet("/keys/{id:guid}/history", KeyHistoryAsync);
        group.MapPost("/keys/{id:guid}/revoke", RevokeKeyAsync);
        group.MapPost("/keys/{id:guid}/block", BlockKeyAsync);
        group.MapPost("/keys/{id:guid}/unblock", UnblockKeyAsync);
        group.MapPost("/keys/{id:guid}/reset-device", ResetDeviceAsync);
        group.MapPost("/keys/{id:guid}/release", ReleaseKeyAsync);
        group.MapGet("/users", SearchUsersAsync);
        group.MapPost("/users/{id:guid}/block", BlockUserAsync);
        group.MapPost("/users/{id:guid}/unblock", UnblockUserAsync);
    }

    private static async Task<IResult> CreateKeyAsync(
        CreateKeyRequest request, HttpContext http, FlashFixDb db, SecretCodec codec)
    {
        var admin = await AdminAsync(http, db);
        if (admin is null) return Results.Unauthorized();
        if (request.DurationDays is not (null or 1 or 7 or 30))
            return Results.BadRequest(new { error = "Duração inválida." });

        var raw = SecretCodec.NewKey();
        var key = new LicenseKey
        {
            Prefix = raw[..11],
            KeyHash = codec.HashKey(raw),
            DurationDays = request.DurationDays
        };
        db.LicenseKeys.Add(key);
        Audit(db, admin.User.Id, key.Id, "license.create", "success");
        await db.SaveChangesAsync();
        return Results.Created($"/v1/admin/keys/{key.Id}", new
        {
            key.Id, Key = raw, key.DurationDays, key.CreatedAt,
            Message = "Guarde esta chave agora. O valor completo não será exibido novamente."
        });
    }

    private static async Task<IResult> SearchKeysAsync(
        string? key, string? username, HttpContext http, FlashFixDb db, SecretCodec codec)
    {
        if (await AdminAsync(http, db) is null) return Results.Unauthorized();
        var query = db.LicenseKeys.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(key))
        {
            if (!InputRules.Key(key)) return Results.BadRequest(new { error = "Formato de chave inválido." });
            var hash = codec.HashKey(key);
            query = query.Where(x => x.KeyHash == hash);
        }
        if (!string.IsNullOrWhiteSpace(username))
        {
            var normalized = InputRules.NormalizeUsername(username);
            query = query.Where(x => x.AssignedUser != null &&
                x.AssignedUser.NormalizedUsername == normalized);
        }

        var keys = await query.Include(x => x.AssignedUser)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        return Results.Ok(keys.Select(x => new
        {
            x.Id, x.Prefix, x.DurationDays, x.CreatedAt, x.ActivatedAt,
            x.ExpiresAt, x.RevokedAt, x.IsBlocked, x.LastLoginAt,
            Username = x.AssignedUser?.Username,
            DeviceBound = x.DeviceHash is not null,
            Status = Status(x)
        }));
    }

    private static async Task<IResult> KeyHistoryAsync(
        Guid id, HttpContext http, FlashFixDb db)
    {
        if (await AdminAsync(http, db) is null) return Results.Unauthorized();
        if (!await db.LicenseKeys.AnyAsync(x => x.Id == id)) return Results.NotFound();
        var events = await db.AuditEvents.AsNoTracking().Where(x => x.LicenseKeyId == id)
            .OrderByDescending(x => x.OccurredAt).Take(100).ToListAsync();
        return Results.Ok(events.Select(x => new
        {
            x.Action, x.Outcome, x.OccurredAt, x.Detail, x.ActorUserId
        }));
    }

    private static Task<IResult> RevokeKeyAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeKeyAsync(id, request, http, db, "license.revoke", (key, now) => key.RevokedAt = now);

    private static Task<IResult> BlockKeyAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeKeyAsync(id, request, http, db, "license.block", (key, _) => key.IsBlocked = true);

    private static Task<IResult> UnblockKeyAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeKeyAsync(id, request, http, db, "license.unblock", (key, _) => key.IsBlocked = false);

    private static Task<IResult> ResetDeviceAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeKeyAsync(id, request, http, db, "license.reset-device", (key, _) => key.DeviceHash = null);

    private static Task<IResult> ReleaseKeyAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeKeyAsync(id, request, http, db, "license.release", (key, _) =>
        {
            key.AssignedUserId = null;
            key.DeviceHash = null;
            key.ActivatedAt = null;
            key.ExpiresAt = null;
            key.LastLoginAt = null;
        });

    private static async Task<IResult> ChangeKeyAsync(
        Guid id, ActionRequest request, HttpContext http, FlashFixDb db,
        string action, Action<LicenseKey, DateTime> change)
    {
        var admin = await AdminAsync(http, db);
        if (admin is null) return Results.Unauthorized();
        if (!ValidReason(request.Reason))
            return Results.BadRequest(new { error = "Informe um motivo entre 3 e 200 caracteres." });

        var key = await db.LicenseKeys.SingleOrDefaultAsync(x => x.Id == id);
        if (key is null) return Results.NotFound();
        var assignedUserId = key.AssignedUserId;
        var now = DateTime.UtcNow;
        change(key, now);
        if (assignedUserId is Guid userId)
            await db.Sessions.Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now));

        Audit(db, admin.User.Id, key.Id, action, "success", request.Reason.Trim());
        await db.SaveChangesAsync();
        return Results.Ok(new { key.Id, Status = Status(key) });
    }

    private static async Task<IResult> SearchUsersAsync(
        string? username, HttpContext http, FlashFixDb db)
    {
        if (await AdminAsync(http, db) is null) return Results.Unauthorized();
        var query = db.Users.AsNoTracking().Where(x => !x.IsAdmin);
        if (!string.IsNullOrWhiteSpace(username))
        {
            var normalized = InputRules.NormalizeUsername(username);
            query = query.Where(x => x.NormalizedUsername == normalized);
        }
        var users = await query.OrderBy(x => x.Username).Take(100).ToListAsync();
        return Results.Ok(users.Select(x => new
        {
            x.Id, x.Username, x.IsBlocked, x.CreatedAt, x.LastLoginAt
        }));
    }

    private static Task<IResult> BlockUserAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeUserAsync(id, request, http, db, true);

    private static Task<IResult> UnblockUserAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db) =>
        ChangeUserAsync(id, request, http, db, false);

    private static async Task<IResult> ChangeUserAsync(Guid id, ActionRequest request,
        HttpContext http, FlashFixDb db, bool blocked)
    {
        var admin = await AdminAsync(http, db);
        if (admin is null) return Results.Unauthorized();
        if (!ValidReason(request.Reason))
            return Results.BadRequest(new { error = "Informe um motivo entre 3 e 200 caracteres." });

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && !x.IsAdmin);
        if (user is null) return Results.NotFound();
        user.IsBlocked = blocked;
        if (blocked)
        {
            var now = DateTime.UtcNow;
            await db.Sessions.Where(x => x.UserId == id && x.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now));
        }
        Audit(db, admin.User.Id, null,
            blocked ? "account.block" : "account.unblock", "success", request.Reason.Trim());
        await db.SaveChangesAsync();
        return Results.Ok(new { user.Id, user.IsBlocked });
    }

    private static async Task<AuthContext?> AdminAsync(HttpContext http, FlashFixDb db)
    {
        var auth = await SessionAuth.ResolveAsync(http, db);
        return auth?.User.IsAdmin == true ? auth : null;
    }

    private static bool ValidReason(string? reason) =>
        reason is { Length: >= 3 and <= 200 };

    private static void Audit(FlashFixDb db, Guid adminId, Guid? keyId,
        string action, string outcome, string? detail = null) => db.AuditEvents.Add(new AuditEvent
    {
        ActorUserId = adminId, LicenseKeyId = keyId,
        Action = action, Outcome = outcome, Detail = detail
    });

    private static string Status(LicenseKey key)
    {
        if (key.RevokedAt is not null) return "revoked";
        if (key.IsBlocked) return "blocked";
        if (key.ExpiresAt <= DateTime.UtcNow) return "expired";
        return key.AssignedUserId is null ? "available" : "active";
    }
}

