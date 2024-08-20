using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Common;

internal static class NextQuest
{
    internal sealed class Factory(QuestRegistry questRegistry, QuestController questController, QuestFunctions questFunctions, ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.CompleteQuest)
                return null;

            if (step.NextQuestId == null)
                return null;

            if (step.NextQuestId == quest.Id)
                return null;

            return new SetQuest(step.NextQuestId, quest.Id, questRegistry, questController, questFunctions, loggerFactory.CreateLogger<SetQuest>());
        }
    }

    private sealed class SetQuest(ElementId nextQuestId, ElementId currentQuestId, QuestRegistry questRegistry, QuestController questController, QuestFunctions questFunctions, ILogger<SetQuest> logger) : ITask
    {
        public bool Start()
        {
            if (questFunctions.IsQuestLocked(nextQuestId, currentQuestId))
            {
                logger.LogInformation("Can't set next quest to {QuestId}, quest is locked", nextQuestId);
            }
            else if (questRegistry.TryGetQuest(nextQuestId, out Quest? quest))
            {
                logger.LogInformation("Setting next quest to {QuestId}: '{QuestName}'", nextQuestId, quest.Info.Name);
                questController.SetNextQuest(quest);
            }
            else
            {
                logger.LogInformation("Next quest with id {QuestId} not found", nextQuestId);
                questController.SetNextQuest(null);
            }

            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"SetNextQuest({nextQuestId})";
    }
}
