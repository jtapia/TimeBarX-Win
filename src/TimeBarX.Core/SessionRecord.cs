namespace TimeBarX.Core;

/// <summary>
/// A single completed-timer entry in the append-only session history log.
/// </summary>
/// <param name="StartedAt">Absolute UTC start time.</param>
/// <param name="CompletedAt">Absolute UTC completion time.</param>
/// <param name="Duration">Configured total duration.</param>
/// <param name="Preset">Preset tag (e.g. "25m") that started the session; may be empty.</param>
/// <param name="Label">Optional user label (from Quick Input or URI).</param>
public sealed record SessionRecord(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    TimeSpan Duration,
    string Preset,
    string? Label = null);
