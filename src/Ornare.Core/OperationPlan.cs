using System.Collections.Generic;

namespace Ornare.Core;

public enum PlanStatus
{
    WouldSucceed = 0,
    WouldFail = 1,
    IndeterminateUntilCommit = 2
}

public sealed record PlanBlocker(string Code, string Message);

public sealed record OperationPlan(
    string Command,
    string Target,
    PlanStatus Status,
    IReadOnlyList<string> PlannedActions,
    IReadOnlyList<PlanBlocker> Blockers,
    IReadOnlyList<string> IndeterminateConditions,
    string? TargetIdentity = null,
    string? SourceMetadata = null,
    string? CacheExpectation = null,
    string? ExpectedArtifact = null,
    long? ExpectedArtifactSizeBytes = null
);
