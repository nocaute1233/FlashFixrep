using System.Text;
using System.Text.RegularExpressions;

namespace FlashFix.Api.Security;

public static partial class InputRules
{
    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$")]
    private static partial Regex UsernameRegex();

    [GeneratedRegex("^[a-fA-F0-9]{32}$")]
    private static partial Regex DeviceRegex();

    [GeneratedRegex("^FF-(?:[a-fA-F0-9]{8}-){3}[a-fA-F0-9]{8}$")]
    private static partial Regex KeyRegex();

    public static bool Username(string? value) =>
        value is not null && UsernameRegex().IsMatch(value);
    public static bool Password(string? value) => value is not null &&
        value.Length >= 12 && Encoding.UTF8.GetByteCount(value) <= 72;
    public static bool Device(string? value) =>
        value is not null && DeviceRegex().IsMatch(value);
    public static bool Key(string? value) =>
        value is not null && KeyRegex().IsMatch(value);
    public static string NormalizeUsername(string value) => value.Trim().ToUpperInvariant();
}
