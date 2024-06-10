using System;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.BaseFactory;

internal static class SkipCondition
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.SkipIf.Contains(ESkipCondition.Never))
                return null;

            if (step.SkipIf.Count == 0 && step.CompletionQuestVariablesFlags.Count == 0)
                return null;

            return serviceProvider.GetRequiredService<CheckTask>()
                .With(step, quest.QuestId);
        }
    }

    internal sealed class CheckTask(
        ILogger<CheckTask> logger,
        GameFunctions gameFunctions) : ITask
    {
        public QuestStep Step { get; set; } = null!;
        public ushort QuestId { get; set; }

        public ITask With(QuestStep step, ushort questId)
        {
            Step = step;
            QuestId = questId;
            return this;
        }

        public bool Start()
        {
            logger.LogInformation("Checking skip conditions; {ConfiguredConditions}", string.Join(",", Step.SkipIf));

            if (Step.SkipIf.Contains(ESkipCondition.FlyingUnlocked) &&
                gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is unlocked");
                return true;
            }

            if (Step.SkipIf.Contains(ESkipCondition.FlyingLocked) &&
                !gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is locked");
                return true;
            }

            if (Step is
                {
                    DataId: not null,
                    InteractionType: EInteractionType.AttuneAetheryte or EInteractionType.AttuneAethernetShard
                } &&
                gameFunctions.IsAetheryteUnlocked((EAetheryteLocation)Step.DataId.Value))
            {
                logger.LogInformation("Skipping step, as aetheryte/aethernet shard is unlocked");
                return true;
            }

            if (Step is { DataId: not null, InteractionType: EInteractionType.AttuneAetherCurrent } &&
                gameFunctions.IsAetherCurrentUnlocked(Step.DataId.Value))
            {
                logger.LogInformation("Skipping step, as current is unlocked");
                return true;
            }

            QuestWork? questWork = gameFunctions.GetQuestEx(QuestId);
            if (questWork != null && Step.MatchesQuestVariables(questWork.Value))
            {
                logger.LogInformation("Skipping step, as quest variables match");
                return true;
            }

            return false;
        }

        public ETaskResult Update() => ETaskResult.SkipRemainingTasksForStep;

        public override string ToString() => $"CheckSkip({string.Join(", ", Step.SkipIf)})";
    }
}
