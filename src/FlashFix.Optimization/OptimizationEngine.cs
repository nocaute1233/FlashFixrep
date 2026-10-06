using System.Text.Json;

namespace FlashFix.Optimization;

public sealed record BackupRecord(
    string TweakId, int[] Original, int[] AppliedValue,
    DateTimeOffset CapturedAt, string State);

public sealed record HistoryEntry(
    string TweakId, string Action, string Outcome,
    DateTimeOffset OccurredAt, string Detail);

public sealed record TweakStatus(
    TweakDefinition Definition, int[] Current, bool MatchesTarget,
    bool CanRestore, string Label);

public sealed record OperationResult(bool Success, string Message);

public sealed class OptimizationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IInputSettings _settings;
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OptimizationEngine(IInputSettings settings, string root)
    {
        _settings = settings;
        _root = Path.GetFullPath(root);
    }

    public static OptimizationEngine CreateDefault() => new(new WindowsInputSettings(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlashFix", "optimizations"));

    public Task<TweakStatus> GetStatusAsync(string id) => Task.Run(() =>
    {
        var definition = TweakCatalog.Get(id);
        var current = _settings.Read(definition.Kind);
        var backup = ReadBackup(id);
        var matches = current.SequenceEqual(definition.Target);
        var canRestore = backup?.State is "pending" or "applied" &&
            (current.SequenceEqual(backup.AppliedValue) || current.SequenceEqual(backup.Original));
        var label = canRestore ? "Aplicado pelo FlashFix" :
            matches ? "Já configurado no Windows" :
            backup?.State is "pending" or "applied" ? "Alterado fora do FlashFix" : "Não aplicado";
        return new TweakStatus(definition, current, matches, canRestore, label);
    });

    public async Task<OperationResult> ApplyAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() => ApplyCore(TweakCatalog.Get(id)));
        }
        finally { _gate.Release(); }
    }

    public async Task<OperationResult> RestoreAsync(string id, bool confirmExternalChange = false)
    {
        await _gate.WaitAsync();
        try
        {
            return await Task.Run(() => RestoreCore(TweakCatalog.Get(id), confirmExternalChange));
        }
        finally { _gate.Release(); }
    }

    public Task<IReadOnlyList<HistoryEntry>> GetHistoryAsync() => Task.Run<IReadOnlyList<HistoryEntry>>(() =>
    {
        var path = Path.Combine(_root, "history.jsonl");
        if (!File.Exists(path)) return [];
        var entries = new List<HistoryEntry>();
        foreach (var line in File.ReadLines(path))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions);
                if (entry is not null) entries.Add(entry);
            }
            catch (JsonException) { /* Preserve the remaining audit entries. */ }
        }
        return entries.OrderByDescending(x => x.OccurredAt).ToList();
    });

    private OperationResult ApplyCore(TweakDefinition definition)
    {
        var current = _settings.Read(definition.Kind);
        var existing = ReadBackup(definition.Id);
        if (existing?.State is "pending" or "applied")
            return new(false, "Já existe um backup ativo. Restaure a configuração antes de aplicar novamente.");
        if (current.SequenceEqual(definition.Target))
            return new(true, "Esta configuração já está ativa no Windows. Nada foi alterado.");

        var backup = new BackupRecord(definition.Id, current, definition.Target,
            DateTimeOffset.UtcNow, "pending");
        WriteBackup(backup); // Persist the original value before touching Windows.
        try
        {
            _settings.Write(definition.Kind, definition.Target);
            if (!_settings.Read(definition.Kind).SequenceEqual(definition.Target))
                throw new InvalidOperationException("O Windows não confirmou a alteração.");
            WriteBackup(backup with { State = "applied" });
            Record(definition.Id, "apply", "success", "Valor original salvo; alteração confirmada.");
            return new(true, "Aplicado com sucesso. O valor original foi salvo para restauração.");
        }
        catch (Exception error)
        {
            try
            {
                _settings.Write(definition.Kind, current);
                if (_settings.Read(definition.Kind).SequenceEqual(current))
                    WriteBackup(backup with { State = "restored" });
            }
            catch { /* Keep the pending backup so the user can retry restore. */ }
            Record(definition.Id, "apply", "failure", error.Message);
            return new(false, $"Falha ao aplicar: {error.Message}");
        }
    }

    private OperationResult RestoreCore(TweakDefinition definition, bool confirmExternalChange)
    {
        var backup = ReadBackup(definition.Id);
        if (backup?.State is not ("pending" or "applied"))
            return new(false, "Não há backup ativo para este ajuste.");
        var current = _settings.Read(definition.Kind);
        if (!confirmExternalChange && !current.SequenceEqual(backup.AppliedValue) &&
            !current.SequenceEqual(backup.Original))
            return new(false, "A configuração foi alterada fora do FlashFix. A restauração automática foi interrompida.");
        try
        {
            if (!current.SequenceEqual(backup.Original))
                _settings.Write(definition.Kind, backup.Original);
            if (!_settings.Read(definition.Kind).SequenceEqual(backup.Original))
                throw new InvalidOperationException("O Windows não confirmou a restauração.");
            WriteBackup(backup with { State = "restored" });
            Record(definition.Id, "restore", "success", "Valor original restaurado.");
            return new(true, "Configuração original restaurada.");
        }
        catch (Exception error)
        {
            Record(definition.Id, "restore", "failure", error.Message);
            return new(false, $"Falha ao restaurar: {error.Message}");
        }
    }

    private BackupRecord? ReadBackup(string id)
    {
        var path = BackupPath(id);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<BackupRecord>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    private void WriteBackup(BackupRecord backup)
    {
        Directory.CreateDirectory(_root);
        var target = BackupPath(backup.TweakId);
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(backup, JsonOptions));
            File.Move(temp, target, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void Record(string id, string action, string outcome, string detail)
    {
        try
        {
            Directory.CreateDirectory(_root);
            var entry = new HistoryEntry(id, action, outcome, DateTimeOffset.UtcNow, detail);
            File.AppendAllText(Path.Combine(_root, "history.jsonl"),
                JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        }
        catch (IOException) { /* A saved backup remains the source of truth. */ }
        catch (UnauthorizedAccessException) { }
    }

    private string BackupPath(string id) => Path.Combine(_root, id + ".json");
}
