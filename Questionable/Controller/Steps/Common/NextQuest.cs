using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.V1;

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

            return serviceProvider.GetRequiredService<SetQuest>()
                .With(step.NextQuestId.Value);
        }
    }

    internal sealed class SetQuest(QuestRegistry questRegistry, QuestController questController, ILogger<SetQuest> logger) : ITask
    {
        public ushort NextQuestId { get; set; }

        public ITask With(ushort nextQuestId)
        {
            NextQuestId = nextQuestId;
            return this;
        }

        public bool Start()
        {
            if (questRegistry.TryGetQuest(NextQuestId, out Quest? quest))
            {
                logger.LogInformation("Setting next quest to {QuestId}: '{QuestName}'", NextQuestId, quest.Info.Name);
                questController.SetNextQuest(quest);
            }
            else
            {
                logger.LogInformation("Next quest with id {QuestId} not found", NextQuestId);
                questController.SetNextQuest(null);
            }

            return true;
        }

        public ETaskResult Update() => ETaskResult.TaskComplete;

        public override string ToString() => $"SetNextQuest({NextQuestId})";
    }
}
