using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlashFix.Client;

public sealed record SessionTokens(string AccessToken, string RefreshToken, DateTime AccessExpiresAt);
public sealed record LicenseInfo(Guid Id, DateTime? ActivatedAt, DateTime? ExpiresAt, string Status);
public sealed record Profile(Guid Id, string Username, bool IsAdmin, LicenseInfo? License);
public sealed record CreatedKey(Guid Id, string Key, int? DurationDays, DateTime CreatedAt, string Message);
public sealed record KeySummary(Guid Id, string Prefix, int? DurationDays, DateTime CreatedAt,
    DateTime? ActivatedAt, DateTime? ExpiresAt, DateTime? RevokedAt, bool IsBlocked,
    DateTime? LastLoginAt, string? Username, bool DeviceBound, string Status);
public sealed record KeyEvent(string Action, string Outcome, DateTime OccurredAt,
    string? Detail, Guid? ActorUserId);
public sealed record UserSummary(Guid Id, string Username, bool IsBlocked,
    DateTime CreatedAt, DateTime? LastLoginAt);
public sealed record DeviceChallenge(string ChallengeId, string Payload);

public sealed class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed class FlashFixApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IDeviceProofProvider? _deviceProof;
    private SessionTokens? _tokens;
    private string? _username;

    public string DeviceId { get; }
    public bool HasSession => _tokens is not null;

    public FlashFixApiClient(string? address = null, string? deviceId = null,
        IDeviceProofProvider? deviceProof = null)
    {
        address ??= Environment.GetEnvironmentVariable("FLASHFIX_API_URL") ?? "http://127.0.0.1:5031";
        var uri = new Uri(address, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp))
            throw new ArgumentException("A API requer HTTPS fora de localhost.", nameof(address));

        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(12) };
        DeviceId = deviceId ?? InstallationIdProvider.GetOrCreate();
        _deviceProof = deviceProof;
    }

    public async Task RegisterAsync(string username, string password, string key)
    {
        if (_deviceProof is null)
            throw new InvalidOperationException("Este dispositivo não oferece a confirmação necessária para ativar a licença.");
        var proof = await CreateProofAsync(username, "register");
        _tokens = await PostAsync<SessionTokens>("/v1/auth/register",
            new { username, password, key, deviceId = DeviceId, proof.ChallengeId,
                devicePublicKey = _deviceProof.PublicKey, deviceSignature = proof.Signature });
        _username = username;
    }

    public async Task LoginAsync(string username, string password)
    {
        (string ChallengeId, string Signature)? proof = _deviceProof is null ? null :
            await CreateProofAsync(username, "login");
        _tokens = await PostAsync<SessionTokens>("/v1/auth/login",
            new { username, password, deviceId = DeviceId, challengeId = proof?.ChallengeId,
                devicePublicKey = _deviceProof?.PublicKey, deviceSignature = proof?.Signature });
        _username = username;
    }

    public Task<Profile> GetProfileAsync() => SendAuthorizedAsync<Profile>(HttpMethod.Get, "/v1/auth/me");

    public async Task LogoutAsync()
    {
        try
        {
            if (_tokens is not null)
                await SendAuthorizedAsync<object>(HttpMethod.Post, "/v1/auth/logout", new { });
        }
        finally { _tokens = null; _username = null; }
    }

    public Task<CreatedKey> CreateKeyAsync(int? durationDays) =>
        SendAuthorizedAsync<CreatedKey>(HttpMethod.Post, "/v1/admin/keys", new { durationDays });

    public Task<List<KeySummary>> SearchKeysAsync(string? key = null, string? username = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(key)) query.Add("key=" + Uri.EscapeDataString(key));
        if (!string.IsNullOrWhiteSpace(username)) query.Add("username=" + Uri.EscapeDataString(username));
        var path = "/v1/admin/keys" + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        return SendAuthorizedAsync<List<KeySummary>>(HttpMethod.Get, path);
    }

    public Task<List<UserSummary>> SearchUsersAsync(string? username = null) =>
        SendAuthorizedAsync<List<UserSummary>>(HttpMethod.Get,
            "/v1/admin/users" + (string.IsNullOrWhiteSpace(username)
                ? "" : "?username=" + Uri.EscapeDataString(username)));

    public Task<List<KeyEvent>> GetKeyHistoryAsync(Guid id) =>
        SendAuthorizedAsync<List<KeyEvent>>(HttpMethod.Get, $"/v1/admin/keys/{id}/history");

    public Task ApplyKeyActionAsync(Guid id, string action, string reason) =>
        SendAuthorizedAsync<object>(HttpMethod.Post,
            $"/v1/admin/keys/{id}/{action}", new { reason });

    public Task ApplyUserActionAsync(Guid id, string action, string reason) =>
        SendAuthorizedAsync<object>(HttpMethod.Post,
            $"/v1/admin/users/{id}/{action}", new { reason });

    private async Task<T> PostAsync<T>(string path, object body)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions);
        return await ReadAsync<T>(response);
    }

    private async Task<(string ChallengeId, string Signature)> CreateProofAsync(string username, string purpose)
    {
        var challenge = await PostAsync<DeviceChallenge>("/v1/auth/device-challenge",
            new { username, purpose });
        var payload = Convert.FromBase64String(challenge.Payload);
        return (challenge.ChallengeId, _deviceProof!.Sign(payload));
    }

    private async Task<T> SendAuthorizedAsync<T>(HttpMethod method, string path, object? body = null)
    {
        await EnsureAccessAsync();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await _http.SendAsync(request);
        return await ReadAsync<T>(response);
    }

    private async Task EnsureAccessAsync()
    {
        if (_tokens is null) throw new ApiException(HttpStatusCode.Unauthorized, "Entre na sua conta primeiro.");
        if (_tokens.AccessExpiresAt > DateTime.UtcNow.AddSeconds(30)) return;
        var refresh = _tokens.RefreshToken;
        try
        {
            (string ChallengeId, string Signature)? proof = _deviceProof is null || _username is null ? null :
                await CreateProofAsync(_username, "refresh");
            _tokens = await PostAsync<SessionTokens>("/v1/auth/refresh",
                new { refreshToken = refresh, deviceId = DeviceId, challengeId = proof?.ChallengeId,
                    devicePublicKey = _deviceProof?.PublicKey, deviceSignature = proof?.Signature });
        }
        catch
        {
            _tokens = null;
            _username = null;
            throw;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            string message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Dados de acesso ou sessão inválidos.",
                HttpStatusCode.TooManyRequests => "Muitas tentativas. Tente novamente mais tarde.",
                _ => "Não foi possível concluir a solicitação."
            };
            try
            {
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (json.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String)
                    message = error.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new ApiException(response.StatusCode, message);
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
            return default!;

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
            ?? throw new InvalidOperationException("Resposta vazia da API.");
    }

    public void Dispose() => _http.Dispose();
}
