namespace OpenApiReport.Core;

public enum ChangeSeverity
{
    Breaking = 0,
    Risky = 1,
    Additive = 2,
    Cosmetic = 3
}

public sealed class ChangeRecord
{
    public required ChangeSeverity Severity { get; init; }
    public required int RiskScore { get; init; }
    public required string Tag { get; init; }
    public required string Endpoint { get; init; }
    public required string Pointer { get; init; }
    public required string Title { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public required string Meaning { get; init; }
    public required string SuggestedAction { get; init; }
}

public sealed class DiffSummary
{
    public int Breaking { get; init; }
    public int Risky { get; init; }
    public int Additive { get; init; }
    public int Cosmetic { get; init; }
    public int Total { get; init; }
    public int TopByRisk { get; init; }
}

public sealed class DiffResult
{
    public required DiffSummary Summary { get; init; }
    public required IReadOnlyList<ChangeRecord> Changes { get; init; }
}
