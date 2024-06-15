using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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

            var relevantConditions =
                step.SkipIf.Where(x => x != ESkipCondition.AetheryteShortcutIfInSameTerritory).ToList();
            if (relevantConditions.Count == 0 && step.CompletionQuestVariablesFlags.Count == 0)
                return null;

            return serviceProvider.GetRequiredService<CheckTask>()
                .With(step, relevantConditions, quest.QuestId);
        }
    }

    internal sealed class CheckTask(
        ILogger<CheckTask> logger,
        GameFunctions gameFunctions) : ITask
    {
        public QuestStep Step { get; set; } = null!;
        public List<ESkipCondition> SkipConditions { get; set; } = null!;
        public ushort QuestId { get; set; }

        public ITask With(QuestStep step, List<ESkipCondition> skipConditions, ushort questId)
        {
            Step = step;
            SkipConditions = skipConditions;
            QuestId = questId;
            return this;
        }

        public unsafe bool Start()
        {
            logger.LogInformation("Checking skip conditions; {ConfiguredConditions}", string.Join(",", SkipConditions));

            if (SkipConditions.Contains(ESkipCondition.FlyingUnlocked) &&
                gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is unlocked");
                return true;
            }

            if (SkipConditions.Contains(ESkipCondition.FlyingLocked) &&
                !gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is locked");
                return true;
            }

            if (SkipConditions.Contains(ESkipCondition.ChocoboUnlocked) &&
                PlayerState.Instance()->IsMountUnlocked(1))
            {
                logger.LogInformation("Skipping step, as chocobo is unlocked");
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
            if (questWork != null && Step.MatchesQuestVariables(questWork.Value, true))
            {
                logger.LogInformation("Skipping step, as quest variables match");
                return true;
            }

            return false;
        }

        public ETaskResult Update() => ETaskResult.SkipRemainingTasksForStep;

        public override string ToString() => $"CheckSkip({string.Join(", ", SkipConditions)})";
    }
}
