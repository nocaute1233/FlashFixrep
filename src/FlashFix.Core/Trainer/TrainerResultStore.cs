using System.Text.Json;

namespace FlashFix.Core.Trainer;

public sealed class TrainerResultStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.GetFullPath(path);

    public static TrainerResultStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlashFix", "trainer-results.jsonl"));

    public void Add(TrainerResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.AppendAllText(_path, JsonSerializer.Serialize(result, JsonOptions) + Environment.NewLine);
    }

    public IReadOnlyList<TrainerResult> Latest(int count = 10)
    {
        if (!File.Exists(_path)) return [];
        var results = new List<TrainerResult>();
        foreach (var line in File.ReadLines(_path))
        {
            try
            {
                var result = JsonSerializer.Deserialize<TrainerResult>(line, JsonOptions);
                if (result is not null) results.Add(result);
            }
            catch (JsonException) { /* A bad line does not hide earlier sessions. */ }
        }
        return results.OrderByDescending(x => x.FinishedAt).Take(count).ToList();
    }
}
