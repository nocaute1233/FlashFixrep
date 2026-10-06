using System.Text.Json;

namespace FlashFix.Core.Crosshair;

public sealed class CrosshairStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string _path = Path.GetFullPath(path);

    public static CrosshairStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlashFix", "crosshairs.json"));

    public IReadOnlyList<CrosshairProfile> Load()
    {
        if (!File.Exists(_path)) return [];
        var profiles = JsonSerializer.Deserialize<List<CrosshairProfile>>(
            File.ReadAllText(_path), JsonOptions) ?? [];
        foreach (var profile in profiles) profile.Validate();
        return profiles;
    }

    public void Save(CrosshairProfile profile)
    {
        profile.Validate();
        var profiles = Load().ToList();
        var index = profiles.FindIndex(x => x.Id == profile.Id);
        if (index >= 0) profiles[index] = profile;
        else
        {
            if (profiles.Count >= 50)
                throw new InvalidOperationException("O limite de 50 perfis foi atingido.");
            profiles.Add(profile);
        }
        Write(profiles);
    }

    public void Delete(Guid id)
    {
        var profiles = Load().Where(x => x.Id != id).ToList();
        Write(profiles);
    }

    private void Write(List<CrosshairProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(profiles, JsonOptions));
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
