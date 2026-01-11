using System.Text;
using System.Text.Json;

namespace OpenApiReport.Core;

public static class ReportFormatters
{
    private static readonly ChangeSeverity[] SeverityOrder =
    {
        ChangeSeverity.Breaking,
        ChangeSeverity.Risky,
        ChangeSeverity.Additive,
        ChangeSeverity.Cosmetic
    };

    public static DiffSummary BuildSummary(IReadOnlyList<ChangeRecord> changes, int topCount = 5)
    {
        var breaking = changes.Count(change => change.Severity == ChangeSeverity.Breaking);
        var risky = changes.Count(change => change.Severity == ChangeSeverity.Risky);
        var additive = changes.Count(change => change.Severity == ChangeSeverity.Additive);
        var cosmetic = changes.Count(change => change.Severity == ChangeSeverity.Cosmetic);
        var total = changes.Count;

        return new DiffSummary
        {
            Breaking = breaking,
            Risky = risky,
            Additive = additive,
            Cosmetic = cosmetic,
            Total = total,
            TopByRisk = Math.Min(topCount, total)
        };
    }

    public static string FormatText(DiffSummary summary, IReadOnlyList<ChangeRecord> changes)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"breaking={summary.Breaking}");
        builder.AppendLine($"risky={summary.Risky}");
        builder.AppendLine($"additive={summary.Additive}");
        builder.AppendLine($"cosmetic={summary.Cosmetic}");
        builder.AppendLine($"total={summary.Total}");
        builder.AppendLine($"top_by_risk={summary.TopByRisk}");
        builder.AppendLine();

        AppendGroupedDetails(builder, changes, isMarkdown: false);
        return builder.ToString();
    }

    public static string FormatMarkdown(DiffSummary summary, IReadOnlyList<ChangeRecord> changes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# OpenAPI Change Report");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine($"- Breaking: {summary.Breaking}");
        builder.AppendLine($"- Risky: {summary.Risky}");
        builder.AppendLine($"- Additive: {summary.Additive}");
        builder.AppendLine($"- Cosmetic: {summary.Cosmetic}");
        builder.AppendLine($"- Total: {summary.Total}");
        builder.AppendLine();

        var topChanges = changes.OrderByDescending(change => change.RiskScore).ThenBy(change => change.Title, StringComparer.Ordinal).Take(5).ToList();
        builder.AppendLine("## Top changes");
        if (topChanges.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var change in topChanges)
            {
                builder.AppendLine($"- **{change.Title}** ({change.Severity}) — `{change.Endpoint}` (Risk {change.RiskScore})");
            }
        }

        builder.AppendLine();
        AppendGroupedDetails(builder, changes, isMarkdown: true);
        return builder.ToString();
    }

    public static string FormatJson(DiffSummary summary, IReadOnlyList<ChangeRecord> changes)
    {
        var payload = new
        {
            summary = summary,
            changes = changes
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static string FormatSummary(DiffSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"breaking={summary.Breaking}");
        builder.AppendLine($"risky={summary.Risky}");
        builder.AppendLine($"additive={summary.Additive}");
        builder.AppendLine($"cosmetic={summary.Cosmetic}");
        builder.AppendLine($"total={summary.Total}");
        builder.AppendLine($"top_by_risk={summary.TopByRisk}");
        return builder.ToString();
    }

    private static void AppendGroupedDetails(StringBuilder builder, IReadOnlyList<ChangeRecord> changes, bool isMarkdown)
    {
        var orderedChanges = changes
            .OrderBy(change => Array.IndexOf(SeverityOrder, change.Severity))
            .ThenBy(change => change.Tag, StringComparer.Ordinal)
            .ThenBy(change => change.Endpoint, StringComparer.Ordinal)
            .ThenBy(change => change.Title, StringComparer.Ordinal)
            .ToList();

        var severityGroups = orderedChanges.GroupBy(change => change.Severity);
        foreach (var severityGroup in severityGroups)
        {
            if (isMarkdown)
            {
                builder.AppendLine($"## {severityGroup.Key}");
            }
            else
            {
                builder.AppendLine($"[{severityGroup.Key}]");
            }

            foreach (var tagGroup in severityGroup.GroupBy(change => change.Tag))
            {
                if (isMarkdown)
                {
                    builder.AppendLine($"### Tag: {tagGroup.Key}");
                }
                else
                {
                    builder.AppendLine($"  Tag: {tagGroup.Key}");
                }

                foreach (var endpointGroup in tagGroup.GroupBy(change => change.Endpoint))
                {
                    if (isMarkdown)
                    {
                        builder.AppendLine($"#### {endpointGroup.Key}");
                    }
                    else
                    {
                        builder.AppendLine($"    {endpointGroup.Key}");
                    }

                    foreach (var change in endpointGroup)
                    {
                        if (isMarkdown)
                        {
                            builder.AppendLine($"- **{change.Title}**");
                            builder.AppendLine($"  - Pointer: `{change.Pointer}`");
                            builder.AppendLine($"  - Before: `{change.Before}` → After: `{change.After}`");
                            builder.AppendLine($"  - Meaning: {change.Meaning}");
                            builder.AppendLine($"  - SuggestedAction: {change.SuggestedAction}");
                            builder.AppendLine($"  - RiskScore: {change.RiskScore}");
                        }
                        else
                        {
                            builder.AppendLine($"      - {change.Title}");
                            builder.AppendLine($"        Pointer: {change.Pointer}");
                            builder.AppendLine($"        Before: {change.Before} -> After: {change.After}");
                            builder.AppendLine($"        Meaning: {change.Meaning}");
                            builder.AppendLine($"        SuggestedAction: {change.SuggestedAction}");
                            builder.AppendLine($"        RiskScore: {change.RiskScore}");
                        }
                    }
                }
            }

            builder.AppendLine();
        }
    }
}
