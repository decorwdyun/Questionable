using System.Collections.Generic;
using System.Linq;
using Questionable.Model;

namespace Questionable.Validation.Validators;

internal sealed class NextQuestValidator : IQuestValidator
{
    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        foreach (var invalidNextQuest in quest.AllSteps().Where(x => x.Step.NextQuestId == quest.QuestElementId))
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestElementId,
                Sequence = (byte)invalidNextQuest.Sequence.Sequence,
                Step = invalidNextQuest.StepId,
                Type = EIssueType.InvalidNextQuestId,
                Severity = EIssueSeverity.Error,
                Description = "Next quest should not reference itself",
            };
        }
    }
}
