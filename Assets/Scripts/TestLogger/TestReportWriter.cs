using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// TestSession을 AI 분석용 Markdown 리포트로 변환/저장.
///
/// 출력 위치:
///   에디터: {ProjectRoot}/TestReports/
///   빌드:   {persistentDataPath}/TestReports/
/// </summary>
public static class TestReportWriter
{
    public static string GetReportDirectory()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "..", "TestReports");
#else
        return Path.Combine(Application.persistentDataPath, "TestReports");
#endif
    }

    public static void WriteReport(TestSession session)
    {
        string dir = GetReportDirectory();
        Directory.CreateDirectory(dir);

        string fileName = $"test_{session.sessionId}_{session.startTimeUtc.Replace(" ", "_").Replace(":", "")}.md";
        string path = Path.Combine(dir, fileName);

        var sb = new StringBuilder();

        // === Header ===
        sb.AppendLine("---");
        sb.AppendLine($"session_id: {session.sessionId}");
        sb.AppendLine($"start_utc: {session.startTimeUtc}");
        sb.AppendLine($"end_utc: {session.endTimeUtc}");
        sb.AppendLine($"duration_sec: {session.durationSeconds:F1}");
        sb.AppendLine($"platform: {session.platform}");
        sb.AppendLine($"build: {session.buildType}");
        sb.AppendLine($"resolution: {session.screenResolution}");
        sb.AppendLine("---");
        sb.AppendLine();

        // === Summary ===
        sb.AppendLine("# Test Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| Highest Stage | {session.highestStageReached} |");
        sb.AppendLine($"| Total Attempts | {session.totalAttempts} |");
        sb.AppendLine($"| Total Failures | {session.totalFailures} |");
        sb.AppendLine($"| Duration | {session.durationSeconds:F1}s |");
        sb.AppendLine();

        // === Failure Breakdown ===
        if (session.failureReasons.Count > 0)
        {
            sb.AppendLine("## Failure Breakdown");
            sb.AppendLine();
            sb.AppendLine("| Reason | Count |");
            sb.AppendLine("|--------|-------|");
            foreach (var kv in session.failureReasons.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"| {kv.Key} | {kv.Value} |");
            }
            sb.AppendLine();
        }

        // === Stage Attempts ===
        if (session.stageAttempts.Count > 0)
        {
            sb.AppendLine("## Stage Attempts");
            sb.AppendLine();
            sb.AppendLine("| # | Stage | Result | Duration | Scatter Power | Catch Time Left | Fail Reason |");
            sb.AppendLine("|---|-------|--------|----------|---------------|-----------------|-------------|");
            for (int i = 0; i < session.stageAttempts.Count; i++)
            {
                var a = session.stageAttempts[i];
                string result = a.succeeded ? "PASS" : "FAIL";
                float dur = a.endTime - a.startTime;
                sb.AppendLine($"| {i + 1} | {a.stage} | {result} | {dur:F1}s | {a.scatterPower:F2} | {a.catchTimeRemaining:F2}s | {a.failReason ?? "-"} |");
            }
            sb.AppendLine();
        }

        // === Phase Transitions (filtered) ===
        var phaseEvents = session.events
            .Where(e => e.category == TestEvent.Category.PhaseChange
                     || e.category == TestEvent.Category.StageChange
                     || e.category == TestEvent.Category.Failure
                     || e.category == TestEvent.Category.Scatter
                     || e.category == TestEvent.Category.Catch)
            .ToList();

        if (phaseEvents.Count > 0)
        {
            sb.AppendLine("## Key Events");
            sb.AppendLine();
            sb.AppendLine("| Time | Frame | Category | Event | Detail |");
            sb.AppendLine("|------|-------|----------|-------|--------|");
            foreach (var e in phaseEvents)
            {
                sb.AppendLine(e.ToMarkdownRow());
            }
            sb.AppendLine();
        }

        // === Full Event Log (collapsible) ===
        sb.AppendLine("## Full Event Log");
        sb.AppendLine();
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>Click to expand full log (" + session.events.Count + " events)</summary>");
        sb.AppendLine();
        sb.AppendLine("| Time | Frame | Category | Event | Detail |");
        sb.AppendLine("|------|-------|----------|-------|--------|");
        foreach (var e in session.events)
        {
            sb.AppendLine(e.ToMarkdownRow());
        }
        sb.AppendLine();
        sb.AppendLine("</details>");

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[TestReportWriter] Report saved: {path}");
    }
}
