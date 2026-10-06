namespace FlashFix.Optimization.Tests;

public sealed class EngineTests
{
    [Fact]
    public async Task Apply_saves_original_and_restore_recovers_it()
    {
        using var files = new TestFiles();
        var device = new FakeInputSettings();
        device.Write(TweakKind.MouseSpeed, [7]);
        var engine = new OptimizationEngine(device, files.Root);

        var applied = await engine.ApplyAsync("mouse.speed");
        Assert.True(applied.Success);
        Assert.Equal([10], device.Read(TweakKind.MouseSpeed));
        Assert.True((await engine.GetStatusAsync("mouse.speed")).CanRestore);

        var restored = await engine.RestoreAsync("mouse.speed");
        Assert.True(restored.Success);
        Assert.Equal([7], device.Read(TweakKind.MouseSpeed));
        Assert.False((await engine.GetStatusAsync("mouse.speed")).CanRestore);
        Assert.Equal(["apply", "restore"], (await engine.GetHistoryAsync())
            .OrderBy(x => x.OccurredAt).Select(x => x.Action));
    }

    [Fact]
    public async Task Restore_refuses_to_replace_external_change()
    {
        using var files = new TestFiles();
        var device = new FakeInputSettings();
        device.Write(TweakKind.MouseSpeed, [7]);
        var engine = new OptimizationEngine(device, files.Root);
        Assert.True((await engine.ApplyAsync("mouse.speed")).Success);
        device.Write(TweakKind.MouseSpeed, [14]);

        var restored = await engine.RestoreAsync("mouse.speed");
        Assert.False(restored.Success);
        Assert.Equal([14], device.Read(TweakKind.MouseSpeed));

        var confirmed = await engine.RestoreAsync("mouse.speed", confirmExternalChange: true);
        Assert.True(confirmed.Success);
        Assert.Equal([7], device.Read(TweakKind.MouseSpeed));
    }

    [Fact]
    public async Task Failed_write_keeps_original_and_records_failure()
    {
        using var files = new TestFiles();
        var device = new FakeInputSettings();
        device.Write(TweakKind.KeyboardDelay, [2]);
        var engine = new OptimizationEngine(device, files.Root);
        device.FailNextWrite = true;

        var result = await engine.ApplyAsync("keyboard.delay");
        Assert.False(result.Success);
        Assert.Equal([2], device.Read(TweakKind.KeyboardDelay));
        Assert.Contains(await engine.GetHistoryAsync(), x => x.Outcome == "failure");
    }

    [Fact]
    public async Task Backup_survives_engine_restart()
    {
        using var files = new TestFiles();
        var device = new FakeInputSettings();
        device.Write(TweakKind.KeyboardSpeed, [20]);
        var first = new OptimizationEngine(device, files.Root);
        Assert.True((await first.ApplyAsync("keyboard.speed")).Success);

        var restarted = new OptimizationEngine(device, files.Root);
        Assert.True((await restarted.GetStatusAsync("keyboard.speed")).CanRestore);
        Assert.True((await restarted.RestoreAsync("keyboard.speed")).Success);
        Assert.Equal([20], device.Read(TweakKind.KeyboardSpeed));
    }

    [Fact]
    public async Task Wheel_setting_restores_the_original_page_scroll_mode()
    {
        using var files = new TestFiles();
        var device = new FakeInputSettings();
        device.Write(TweakKind.MouseWheelLines, [-1]);
        var engine = new OptimizationEngine(device, files.Root);

        Assert.True((await engine.ApplyAsync("mouse.wheel-lines")).Success);
        Assert.Equal([3], device.Read(TweakKind.MouseWheelLines));
        Assert.True((await engine.RestoreAsync("mouse.wheel-lines")).Success);
        Assert.Equal([-1], device.Read(TweakKind.MouseWheelLines));
    }

    private sealed class FakeInputSettings : IInputSettings
    {
        private readonly Dictionary<TweakKind, int[]> _values = new();
        public bool FailNextWrite { get; set; }
        public int[] Read(TweakKind kind) =>
            _values.TryGetValue(kind, out var value) ? [.. value] : [1];
        public void Write(TweakKind kind, int[] values)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new InvalidOperationException("Falha simulada");
            }
            _values[kind] = [.. values];
        }
    }

    private sealed class TestFiles : IDisposable
    {
        private static readonly string Base = Path.Combine(Path.GetTempPath(), "FlashFix.Engine.Tests");
        public string Root { get; } = Path.Combine(Base, Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            var allowed = Path.GetFullPath(Base) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup path escaped its directory.");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
}
