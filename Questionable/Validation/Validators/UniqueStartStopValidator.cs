using System.Collections.Generic;
using System.Linq;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Validation.Validators;

internal sealed class UniqueStartStopValidator : IQuestValidator
{
    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        var questAccepts = FindQuestStepsWithInteractionType(quest, EInteractionType.AcceptQuest)
            .Where(x => x.Step.PickUpQuestId == null)
            .ToList();
        foreach (var accept in questAccepts)
        {
            if (accept.Sequence.Sequence != 0 || accept.StepId != quest.FindSequence(0)!.Steps.Count - 1)
            {
                yield return new ValidationIssue
                {
                    QuestId = quest.QuestElementId,
                    Sequence = (byte)accept.Sequence.Sequence,
                    Step = accept.StepId,
                    Type = EIssueType.UnexpectedAcceptQuestStep,
                    Severity = EIssueSeverity.Error,
                    Description = "Unexpected AcceptQuest step",
                };
            }
        }

        if (quest.FindSequence(0) != null && questAccepts.Count == 0)
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestElementId,
                Sequence = 0,
                Step = null,
                Type = EIssueType.MissingQuestAccept,
                Severity = EIssueSeverity.Error,
                Description = "No AcceptQuest step",
            };
        }

        var questCompletes = FindQuestStepsWithInteractionType(quest, EInteractionType.CompleteQuest)
            .Where(x => x.Step.TurnInQuestId == null)
            .ToList();
        foreach (var complete in questCompletes)
        {
            if (complete.Sequence.Sequence != 255 || complete.StepId != quest.FindSequence(255)!.Steps.Count - 1)
            {
                yield return new ValidationIssue
                {
                    QuestId = quest.QuestElementId,
                    Sequence = (byte)complete.Sequence.Sequence,
                    Step = complete.StepId,
                    Type = EIssueType.UnexpectedCompleteQuestStep,
                    Severity = EIssueSeverity.Error,
                    Description = "Unexpected CompleteQuest step",
                };
            }
        }

        if (quest.FindSequence(255) != null && questCompletes.Count == 0)
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestElementId,
                Sequence = 255,
                Step = null,
                Type = EIssueType.MissingQuestComplete,
                Severity = EIssueSeverity.Error,
                Description = "No CompleteQuest step",
            };
        }
    }

    private static IEnumerable<(QuestSequence Sequence, int StepId, QuestStep Step)> FindQuestStepsWithInteractionType(
        Quest quest, EInteractionType interactionType)
        => quest.AllSteps().Where(x => x.Step.InteractionType == interactionType);
}
