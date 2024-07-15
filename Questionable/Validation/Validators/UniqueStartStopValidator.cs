using System.Collections.Generic;
using System.Linq;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Validation.Validators;

internal sealed class UniqueStartStopValidator : IQuestValidator
{
    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        var questAccepts = FindQuestStepsWithInteractionType(quest, EInteractionType.AcceptQuest)
            .Where(x => x.Step.PickupQuestId == null)
            .ToList();
        foreach (var accept in questAccepts)
        {
            if (accept.SequenceId != 0 || accept.StepId != quest.FindSequence(0)!.Steps.Count - 1)
            {
                yield return new ValidationIssue
                {
                    QuestId = quest.QuestId,
                    Sequence = (byte)accept.SequenceId,
                    Step = accept.StepId,
                    Severity = EIssueSeverity.Error,
                    Description = "Unexpected AcceptQuest step",
                };
            }
        }

        if (quest.FindSequence(0) != null && questAccepts.Count == 0)
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestId,
                Sequence = 0,
                Step = null,
                Severity = EIssueSeverity.Error,
                Description = "No AcceptQuest step",
            };
        }

        var questCompletes = FindQuestStepsWithInteractionType(quest, EInteractionType.CompleteQuest)
            .Where(x => x.Step.TurnInQuestId == null)
            .ToList();
        foreach (var complete in questCompletes)
        {
            if (complete.SequenceId != 255 || complete.StepId != quest.FindSequence(255)!.Steps.Count - 1)
            {
                yield return new ValidationIssue
                {
                    QuestId = quest.QuestId,
                    Sequence = (byte)complete.SequenceId,
                    Step = complete.StepId,
                    Severity = EIssueSeverity.Error,
                    Description = "Unexpected CompleteQuest step",
                };
            }
        }

        if (quest.FindSequence(255) != null && questCompletes.Count == 0)
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestId,
                Sequence = 255,
                Step = null,
                Severity = EIssueSeverity.Error,
                Description = "No CompleteQuest step",
            };
        }
    }

    private static IEnumerable<(int SequenceId, int StepId, QuestStep Step)> FindQuestStepsWithInteractionType(Quest quest, EInteractionType interactionType)
    {
        foreach (var sequence in quest.Root.QuestSequence)
        {
            for (int i = 0; i < sequence.Steps.Count; ++i)
            {
                var step = sequence.Steps[i];
                if (step.InteractionType == interactionType)
                    yield return (sequence.Sequence, i, step);
            }
        }
    }
}
