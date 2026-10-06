using System.Threading.RateLimiting;
using FlashFix.Api.Data;
using FlashFix.Api.Endpoints;
using FlashFix.Api.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);

var sqlitePath = builder.Configuration["FLASHFIX_SQLITE_PATH"];
var postgres = builder.Configuration["FLASHFIX_POSTGRES_CONNECTION"];
if (!string.IsNullOrWhiteSpace(postgres))
    builder.Services.AddDbContext<FlashFixDb>(options => options.UseNpgsql(postgres));
else if (builder.Environment.IsDevelopment())
{
    sqlitePath ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlashFix", "dev", "api.db");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);
    builder.Services.AddDbContext<FlashFixDb>(options => options.UseSqlite($"Data Source={sqlitePath}"));
}
else
    throw new InvalidOperationException("FLASHFIX_POSTGRES_CONNECTION is required outside Development.");

builder.Services.AddSingleton<SecretCodec>();
builder.Services.AddSingleton<DeviceChallenges>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("admin", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
_ = app.Services.GetRequiredService<SecretCodec>();

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (http, next) =>
{
    var localDevelopment = app.Environment.IsDevelopment() &&
        http.Connection.RemoteIpAddress is { } address &&
        System.Net.IPAddress.IsLoopback(address);
    if (!http.Request.IsHttps && !localDevelopment)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        await http.Response.WriteAsJsonAsync(new { error = "Esta API requer HTTPS." });
        return;
    }
    await next();
});
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlashFixDb>();
    if (app.Environment.IsDevelopment())
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    var adminName = app.Configuration["FLASHFIX_BOOTSTRAP_ADMIN_USERNAME"];
    var adminPassword = app.Configuration["FLASHFIX_BOOTSTRAP_ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(adminName) && !string.IsNullOrWhiteSpace(adminPassword) &&
        !await db.Users.AnyAsync(x => x.IsAdmin))
    {
        if (!InputRules.Username(adminName) || !InputRules.Password(adminPassword))
            throw new InvalidOperationException("Bootstrap admin credentials do not meet input rules.");
        db.Users.Add(new UserAccount
        {
            Username = adminName,
            NormalizedUsername = InputRules.NormalizeUsername(adminName),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12),
            IsAdmin = true
        });
        await db.SaveChangesAsync();
    }
    Environment.SetEnvironmentVariable("FLASHFIX_BOOTSTRAP_ADMIN_PASSWORD", null);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.Run();
