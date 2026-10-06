using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FlashFix.Client;

public static partial class InstallationIdProvider
{
    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex IdPattern();

    public static string GetOrCreate()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashFix");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "installation.id");
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (IdPattern().IsMatch(existing)) return existing;
            throw new InvalidOperationException("Identificador local do dispositivo inválido.");
        }

        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(value);
            return value;
        }
        catch (IOException) when (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (IdPattern().IsMatch(existing)) return existing;
            throw;
        }
    }
}
