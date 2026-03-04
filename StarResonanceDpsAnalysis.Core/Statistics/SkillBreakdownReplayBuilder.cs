using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Rebuilds a detached SkillBreakdown-ready PlayerStatistics for ONE player from battle logs.
/// Used on demand when opening SkillBreakdown, so we avoid keeping heavy chart data for everyone.
/// </summary>
public static class SkillBreakdownReplayBuilder
{
    public static PlayerStatistics? BuildForPlayer(
        long uid,
        IReadOnlyList<BattleLog> logs,
        int sampleIntervalMs,
        int sampleCapacity)
    {
        if (uid == 0 || logs == null || logs.Count == 0)
        {
            return null;
        }

        var relevantLogs = logs
            .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
            .OrderBy(log => log.TimeTicks)
            .ToList();

        if (relevantLogs.Count == 0)
        {
            return null;
        }

        // Reuse existing calculators for totals / skill breakdown.
        var adapter = new StatisticsAdapter();
        foreach (var log in relevantLogs)
        {
            adapter.ProcessLog(log);
        }

        var sectionStats = adapter.GetStatistics(fullSession: false);
        if (!sectionStats.TryGetValue(uid, out var stats))
        {
            return null;
        }

        // Build projected chart samples only for this selected player.
        BuildProjectedSamples(
            uid,
            relevantLogs,
            Math.Clamp(sampleIntervalMs, 1, 60_000),
            Math.Clamp(sampleCapacity, 50, 1000),
            out var dps,
            out var hps,
            out var dtps);

        stats.SetProjectedSamples(dps, hps, dtps);
        return stats;
    }

    private static void BuildProjectedSamples(
        long uid,
        IReadOnlyList<BattleLog> relevantLogs,
        int sampleIntervalMs,
        int sampleCapacity,
        out List<DpsDataPoint> dps,
        out List<DpsDataPoint> hps,
        out List<DpsDataPoint> dtps)
    {
        dps = new List<DpsDataPoint>(sampleCapacity);
        hps = new List<DpsDataPoint>(sampleCapacity);
        dtps = new List<DpsDataPoint>(sampleCapacity);

        if (relevantLogs.Count == 0)
        {
            return;
        }

        var ordered = relevantLogs.OrderBy(log => log.TimeTicks).ToList();
        var startTick = ordered[0].TimeTicks;
        var intervalTicks = Math.Max(1, sampleIntervalMs) * TimeSpan.TicksPerMillisecond;

        long totalDamage = 0;
        long totalHealing = 0;
        long totalTaken = 0;

        long baselineDamage = 0;
        long baselineHealing = 0;
        long baselineTaken = 0;
        long baselineTick = 0;

        long lastSeenTick = 0;
        long lastRecordedTick = 0;
        long nextBoundary = startTick + intervalTicks;

        var i = 0;
        while (i < ordered.Count)
        {
            var log = ordered[i];

            // Emit timer boundaries BEFORE consuming the current log
            // so logs after the boundary do not affect the earlier sample.
            while (nextBoundary <= log.TimeTicks)
            {
                if (lastSeenTick > 0 && lastSeenTick != lastRecordedTick)
                {
                    if (baselineTick == 0)
                    {
                        // Old behavior: first sample call only initializes baseline.
                        baselineDamage = totalDamage;
                        baselineHealing = totalHealing;
                        baselineTaken = totalTaken;
                        baselineTick = lastSeenTick;
                        lastRecordedTick = lastSeenTick;
                    }
                    else
                    {
                        var seconds = (lastSeenTick - baselineTick) / (double)TimeSpan.TicksPerSecond;
                        if (seconds > 0)
                        {
                            AppendWithCapacity(dps, new DpsDataPoint(
                                TimeSpan.FromTicks(lastSeenTick - startTick),
                                (totalDamage - baselineDamage) / seconds), sampleCapacity);

                            AppendWithCapacity(hps, new DpsDataPoint(
                                TimeSpan.FromTicks(lastSeenTick - startTick),
                                (totalHealing - baselineHealing) / seconds), sampleCapacity);

                            AppendWithCapacity(dtps, new DpsDataPoint(
                                TimeSpan.FromTicks(lastSeenTick - startTick),
                                (totalTaken - baselineTaken) / seconds), sampleCapacity);

                            lastRecordedTick = lastSeenTick;
                        }
                    }
                }

                nextBoundary += intervalTicks;
            }

            ApplyLogToRunningTotals(uid, log, ref totalDamage, ref totalHealing, ref totalTaken);
            lastSeenTick = log.TimeTicks;
            i++;
        }

        // No forced synthetic final sample here.
        // This keeps the replay closer to the "timer-poll" style behavior.
    }

    private static void ApplyLogToRunningTotals(
        long uid,
        BattleLog log,
        ref long totalDamage,
        ref long totalHealing,
        ref long totalTaken)
    {
        // Damage dealt by this player to non-player targets
        if (!log.IsHeal && log.IsAttackerPlayer && !log.IsTargetPlayer && log.AttackerUuid == uid)
        {
            totalDamage += Math.Max(0, log.Value);
        }

        // Healing done by this player to player targets
        if (log.IsHeal && log.IsAttackerPlayer && log.IsTargetPlayer && log.AttackerUuid == uid)
        {
            totalHealing += Math.Max(0, log.Value);
        }

        // Damage taken by this player
        if (!log.IsHeal && log.IsTargetPlayer && log.TargetUuid == uid)
        {
            totalTaken += Math.Max(0, log.Value);
        }
    }

    private static void AppendWithCapacity(List<DpsDataPoint> list, DpsDataPoint point, int capacity)
    {
        list.Add(point);
        if (list.Count > capacity)
        {
            list.RemoveAt(0);
        }
    }
}