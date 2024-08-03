using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Common;

internal static class NextQuest
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.CompleteQuest)
                return null;

            if (step.NextQuestId == null)
                return null;

            if (step.NextQuestId == quest.QuestElementId)
                return null;

            return serviceProvider.GetRequiredService<SetQuest>()
                .With(step.NextQuestId, quest.QuestElementId);
        }
    }

    internal sealed class SetQuest(QuestRegistry questRegistry, QuestController questController, GameFunctions gameFunctions, ILogger<SetQuest> logger) : ITask
    {
        public ElementId NextQuestElementId { get; set; } = null!;
        public ElementId CurrentQuestElementId { get; set; } = null!;

        public ITask With(ElementId nextQuestElementId, ElementId currentQuestElementId)
        {
            NextQuestElementId = nextQuestElementId;
            CurrentQuestElementId = currentQuestElementId;
            return this;
        }

        public bool Start()
        {
            if (gameFunctions.IsQuestLocked(NextQuestElementId, CurrentQuestElementId))
            {
                logger.LogInformation("Can't set next quest to {QuestId}, quest is locked", NextQuestElementId);
            }
            else if (questRegistry.TryGetQuest(NextQuestElementId, out Quest? quest))
            {
                logger.LogInformation("Setting next quest to {QuestId}: '{QuestName}'", NextQuestElementId, quest.Info.Name);
                questController.SetNextQuest(quest);
            }
            else
            {
                logger.LogInformation("Next quest with id {QuestId} not found", NextQuestElementId);
                questController.SetNextQuest(null);
            }

            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"SetNextQuest({NextQuestElementId})";
    }
}
