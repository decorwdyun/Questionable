using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Questionable.Controller.Utils;
using Questionable.Model;

namespace Questionable.Validation.Validators;

internal sealed class CompletionFlagsValidator : IQuestValidator
{
    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        foreach (var sequence in quest.AllSequences())
        {
            var mappedCompletionFlags = sequence.Steps
                .Select(x =>
                {
                    if (QuestWorkUtils.HasCompletionFlags(x.CompletionQuestVariablesFlags))
                    {
                        return Enumerable.Range(0, 6).Select(y =>
                            {
                                short? value = x.CompletionQuestVariablesFlags[y];
                                if (value == null || value.Value < 0)
                                    return 0;
                                return (long)BitOperations.RotateLeft((ulong)value.Value, 8 * y);
                            })
                            .Sum();
                    }
                    else
                        return 0;
                })
                .ToList();

            for (int i = 0; i < sequence.Steps.Count; ++i)
            {
                var flags = mappedCompletionFlags[i];
                if (flags == 0)
                    continue;

                if (mappedCompletionFlags.Count(x => x == flags) >= 2)
                {
                    yield return new ValidationIssue
                    {
                        QuestId = quest.QuestId,
                        Sequence = (byte)sequence.Sequence,
                        Step = i,
                        Type = EIssueType.DuplicateCompletionFlags,
                        Severity = EIssueSeverity.Error,
                        Description = $"Duplicate completion flags: {string.Join(", ", sequence.Steps[i].CompletionQuestVariablesFlags)}",
                    };
                }
            }
        }
    }
}
