using FlashFix.Core.Crosshair;
using FlashFix.Core.Trainer;

namespace FlashFix.Core.Tests;

public sealed class FeatureTests
{
    [Fact]
    public void Crosshair_profile_can_be_saved_reloaded_and_deleted()
    {
        using var files = new TestFiles();
        var store = new CrosshairStore(Path.Combine(files.Root, "crosshairs.json"));
        var profile = CrosshairProfile.Default() with { Name = "Mira de treino", Color = "#00FF88" };
        store.Save(profile);
        Assert.Equal(profile, Assert.Single(store.Load()));
        store.Save(profile with { Gap = 12 });
        Assert.Equal(12, Assert.Single(store.Load()).Gap);
        store.Delete(profile.Id);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Trainer_measures_click_accuracy_and_reaction_time()
    {
        var session = new TrainerSession(TrainerMode.ReflexShot, 30);
        session.RecordClick(false);
        session.RecordClick(true, 200);
        session.RecordClick(true, 300);
        var result = session.Finish();
        Assert.Equal(2, result.Hits);
        Assert.Equal(1, result.Misses);
        Assert.Equal(200, result.Score);
        Assert.Equal(250, result.AverageReactionMs);
        Assert.InRange(result.AccuracyPercent, 66.6, 66.7);
    }

    [Fact]
    public void Tracking_uses_time_samples_instead_of_clicks()
    {
        var session = new TrainerSession(TrainerMode.Tracking, 30);
        session.RecordTrackingSample(true);
        session.RecordTrackingSample(false);
        Assert.Equal(50, session.AccuracyPercent);
        Assert.Equal(5, session.Score);
        Assert.Throws<InvalidOperationException>(() => session.RecordClick(true));
    }

    private sealed class TestFiles : IDisposable
    {
        private static readonly string Base = Path.Combine(Path.GetTempPath(), "FlashFix.Core.Tests");
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
