﻿using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Validation;

internal sealed record ValidationIssue
{
    public required ElementId? QuestId { get; init; }
    public required byte? Sequence { get; init; }
    public required int? Step { get; init; }
    public EBeastTribe BeastTribe { get; init; } = EBeastTribe.None;
    public required EIssueType Type { get; init; }
    public required EIssueSeverity Severity { get; init; }
    public required string Description { get; init; }
}
