namespace FlashFix.Core.Trainer;

public enum TrainerMode { Flick, Tracking, ReflexShot, Gridshot }

public sealed record TrainerResult(
    TrainerMode Mode, DateTimeOffset FinishedAt, int DurationSeconds,
    int Score, int Hits, int Misses, double AccuracyPercent,
    double? AverageReactionMs);

public sealed class TrainerSession(TrainerMode mode, int durationSeconds)
{
    private int _hits;
    private int _misses;
    private int _score;
    private double _reactionTotalMs;
    private int _reactionCount;

    public TrainerMode Mode { get; } = mode;
    public int DurationSeconds { get; } = durationSeconds is >= 10 and <= 180
        ? durationSeconds : throw new ArgumentOutOfRangeException(nameof(durationSeconds));
    public int Hits => _hits;
    public int Misses => _misses;
    public int Score => _score;
    public double AccuracyPercent => _hits + _misses == 0 ? 0 :
        _hits * 100d / (_hits + _misses);

    public void RecordClick(bool hit, double? reactionMs = null)
    {
        if (Mode == TrainerMode.Tracking)
            throw new InvalidOperationException("Tracking usa amostras de posição, não cliques.");
        if (!hit) { _misses++; return; }
        _hits++;
        _score += Mode == TrainerMode.Gridshot ? 75 : 100;
        if (reactionMs is >= 0 and <= 10_000)
        {
            _reactionTotalMs += reactionMs.Value;
            _reactionCount++;
        }
    }

    public void RecordTrackingSample(bool insideTarget)
    {
        if (Mode != TrainerMode.Tracking)
            throw new InvalidOperationException("Amostras são exclusivas do Tracking.");
        if (insideTarget) { _hits++; _score += 5; }
        else _misses++;
    }

    public TrainerResult Finish() => new(Mode, DateTimeOffset.UtcNow,
        DurationSeconds, _score, _hits, _misses, AccuracyPercent,
        _reactionCount == 0 ? null : _reactionTotalMs / _reactionCount);
}
