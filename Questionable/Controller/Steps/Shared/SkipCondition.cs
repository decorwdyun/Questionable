using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Utils;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Shared;

internal static class SkipCondition
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            var skipConditions = step.SkipConditions?.StepIf;
            if (skipConditions is { Never: true })
                return null;

            if ((skipConditions == null || !skipConditions.HasSkipConditions()) &&
                !QuestWorkUtils.HasCompletionFlags(step.CompletionQuestVariablesFlags) &&
                step.RequiredQuestVariables.Count == 0 &&
                step.PickUpQuestId == null &&
                step.NextQuestId == null)
                return null;

            return serviceProvider.GetRequiredService<CheckSkip>()
                .With(step, skipConditions ?? new(), quest.QuestId);
        }
    }

    internal sealed class CheckSkip(
        ILogger<CheckSkip> logger,
        GameFunctions gameFunctions,
        IClientState clientState) : ITask
    {
        public QuestStep Step { get; set; } = null!;
        public SkipStepConditions SkipConditions { get; set; } = null!;
        public ushort QuestId { get; set; }

        public ITask With(QuestStep step, SkipStepConditions skipConditions, ushort questId)
        {
            Step = step;
            SkipConditions = skipConditions;
            QuestId = questId;
            return this;
        }

        public unsafe bool Start()
        {
            logger.LogInformation("Checking skip conditions; {ConfiguredConditions}", string.Join(",", SkipConditions));

            if (SkipConditions.Flying == ELockedSkipCondition.Unlocked &&
                gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is unlocked");
                return true;
            }

            if (SkipConditions.Flying == ELockedSkipCondition.Locked &&
                !gameFunctions.IsFlyingUnlocked(Step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is locked");
                return true;
            }

            if (SkipConditions.Chocobo == ELockedSkipCondition.Unlocked &&
                PlayerState.Instance()->IsMountUnlocked(1))
            {
                logger.LogInformation("Skipping step, as chocobo is unlocked");
                return true;
            }

            if (SkipConditions.QuestsCompleted.Count > 0 &&
                SkipConditions.QuestsCompleted.All(gameFunctions.IsQuestComplete))
            {
                logger.LogInformation("Skipping step, all prequisite quests are complete");
                return true;
            }

            if (SkipConditions.QuestsAccepted.Count > 0 &&
                SkipConditions.QuestsAccepted.All(gameFunctions.IsQuestAccepted))
            {
                logger.LogInformation("Skipping step, all prequisite quests are accepted");
                return true;
            }

            if (SkipConditions.NotTargetable &&
                Step is { DataId: not null })
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(Step.DataId.Value);
                if (gameObject == null)
                {
                    if ((Step.Position.GetValueOrDefault() - clientState.LocalPlayer!.Position).Length() < 100)
                    {
                        logger.LogInformation("Skipping step, object is not nearby (but we are)");
                        return true;
                    }
                }
                else if (!gameObject.IsTargetable)
                {
                    logger.LogInformation("Skipping step, object is not targetable");
                    return true;
                }
            }

            if (SkipConditions.Item is { NotInInventory: true } && Step is { ItemId: not null })
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                if (inventoryManager->GetInventoryItemCount(Step.ItemId.Value) == 0)
                {
                    logger.LogInformation("Skipping step, no item with itemId {ItemId} in inventory",
                        Step.ItemId.Value);
                    return true;
                }
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
            if (questWork != null)
            {
                if (QuestWorkUtils.MatchesQuestWork(Step.CompletionQuestVariablesFlags, questWork.Value, true))
                {
                    logger.LogInformation("Skipping step, as quest variables match");
                    return true;
                }

                if (!QuestWorkUtils.MatchesRequiredQuestWorkConfig(Step.RequiredQuestVariables, questWork.Value,
                        logger))
                {
                    logger.LogInformation("Skipping step, as required variables do not match");
                    return true;
                }
            }

            if (SkipConditions.ExtraCondition == EExtraSkipCondition.WakingSandsMainArea)
            {
                var position = clientState.LocalPlayer!.Position;
                if (position.X < 24)
                {
                    logger.LogInformation("Skipping step, as we're not in the Solar");
                    return true;
                }
            }

            if (Step.PickUpQuestId != null && gameFunctions.IsQuestAcceptedOrComplete(Step.PickUpQuestId.Value))
            {
                logger.LogInformation("Skipping step, as we have already picked up the relevant quest");
                return true;
            }

            if (Step.TurnInQuestId != null && gameFunctions.IsQuestComplete(Step.TurnInQuestId.Value))
            {
                logger.LogInformation("Skipping step, as we have already completed the relevant quest");
                return true;
            }

            return false;
        }

        public ETaskResult Update() => ETaskResult.SkipRemainingTasksForStep;

        public override string ToString() => "CheckSkip";
    }
}
