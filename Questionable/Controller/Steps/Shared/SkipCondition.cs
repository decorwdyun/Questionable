﻿using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Utils;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class SkipCondition
{
    internal sealed class Factory : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
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

            return new SkipTask(step, skipConditions ?? new(), quest.Id);
        }
    }

    internal sealed record SkipTask(
        QuestStep Step,
        SkipStepConditions SkipConditions,
        ElementId ElementId) : ITask
    {
        public override string ToString() => "CheckSkip";
    }

    internal sealed class CheckSkip(
        ILogger<CheckSkip> logger,
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        IClientState clientState) : TaskExecutor<SkipTask>
    {
        protected override unsafe bool Start()
        {
            var skipConditions = Task.SkipConditions;
            var step = Task.Step;
            var elementId = Task.ElementId;

            logger.LogInformation("Checking skip conditions; {ConfiguredConditions}", string.Join(",", skipConditions));

            if (skipConditions.Flying == ELockedSkipCondition.Unlocked &&
                gameFunctions.IsFlyingUnlocked(step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is unlocked");
                return true;
            }

            if (skipConditions.Flying == ELockedSkipCondition.Locked &&
                !gameFunctions.IsFlyingUnlocked(step.TerritoryId))
            {
                logger.LogInformation("Skipping step, as flying is locked");
                return true;
            }

            if (skipConditions.Chocobo == ELockedSkipCondition.Unlocked &&
                PlayerState.Instance()->IsMountUnlocked(1))
            {
                logger.LogInformation("Skipping step, as chocobo is unlocked");
                return true;
            }

            if (skipConditions.InTerritory.Count > 0 &&
                skipConditions.InTerritory.Contains(clientState.TerritoryType))
            {
                logger.LogInformation("Skipping step, as in a skip.InTerritory");
                return true;
            }

            if (skipConditions.NotInTerritory.Count > 0 &&
                !skipConditions.NotInTerritory.Contains(clientState.TerritoryType))
            {
                logger.LogInformation("Skipping step, as not in a skip.NotInTerritory");
                return true;
            }

            if (skipConditions.QuestsCompleted.Count > 0 &&
                skipConditions.QuestsCompleted.All(questFunctions.IsQuestComplete))
            {
                logger.LogInformation("Skipping step, all prequisite quests are complete");
                return true;
            }

            if (skipConditions.QuestsAccepted.Count > 0 &&
                skipConditions.QuestsAccepted.All(questFunctions.IsQuestAccepted))
            {
                logger.LogInformation("Skipping step, all prequisite quests are accepted");
                return true;
            }

            if (skipConditions.NotTargetable &&
                step is { DataId: not null })
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(step.DataId.Value);
                if (gameObject == null)
                {
                    if ((step.Position.GetValueOrDefault() - clientState.LocalPlayer!.Position).Length() < 100)
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

            if (skipConditions.NotNamePlateIconId.Count > 0 &&
                step is { DataId: not null })
            {
                IGameObject? target = gameFunctions.FindObjectByDataId(step.DataId.Value);
                if (target != null)
                {
                    GameObject* gameObject = (GameObject*)target.Address;
                    if (!skipConditions.NotNamePlateIconId.Contains(gameObject->NamePlateIconId))
                    {
                        logger.LogInformation("Skipping step, object has icon id {IconId}", gameObject->NamePlateIconId);
                        return true;
                    }
                }
            }

            if (skipConditions.Item is { NotInInventory: true } && step is { ItemId: not null })
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                if (inventoryManager->GetInventoryItemCount(step.ItemId.Value) == 0 &&
                    inventoryManager->GetInventoryItemCount(step.ItemId.Value, true) == 0)
                {
                    logger.LogInformation("Skipping step, no item with itemId {ItemId} in inventory",
                        step.ItemId.Value);
                    return true;
                }
            }

            if (step is
                {
                    DataId: not null,
                    InteractionType: EInteractionType.AttuneAetheryte or EInteractionType.AttuneAethernetShard
                } &&
                aetheryteFunctions.IsAetheryteUnlocked((EAetheryteLocation)step.DataId.Value))
            {
                logger.LogInformation("Skipping step, as aetheryte/aethernet shard is unlocked");
                return true;
            }

            if (skipConditions.AetheryteLocked != null &&
                !aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteLocked.Value))
            {
                logger.LogInformation("Skipping step, as aetheryte is locked");
                return true;
            }

            if (skipConditions.AetheryteUnlocked != null &&
                aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteUnlocked.Value))
            {
                logger.LogInformation("Skipping step, as aetheryte is unlocked");
                return true;
            }

            if (step is { DataId: not null, InteractionType: EInteractionType.AttuneAetherCurrent } &&
                gameFunctions.IsAetherCurrentUnlocked(step.DataId.Value))
            {
                logger.LogInformation("Skipping step, as current is unlocked");
                return true;
            }

            QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(elementId);
            if (questWork != null)
            {
                if (QuestWorkUtils.HasCompletionFlags(step.CompletionQuestVariablesFlags) &&
                    QuestWorkUtils.MatchesQuestWork(step.CompletionQuestVariablesFlags, questWork))
                {
                    logger.LogInformation("Skipping step, as quest variables match (step is complete)");
                    return true;
                }

                if (step is { SkipConditions.StepIf: { } conditions })
                {
                    if (QuestWorkUtils.MatchesQuestWork(conditions.CompletionQuestVariablesFlags, questWork))
                    {
                        logger.LogInformation("Skipping step, as quest variables match (step can be skipped)");
                        return true;
                    }
                }

                if (step is { RequiredQuestVariables: { } requiredQuestVariables })
                {
                    if (!QuestWorkUtils.MatchesRequiredQuestWorkConfig(requiredQuestVariables, questWork, logger))
                    {
                        logger.LogInformation("Skipping step, as required variables do not match");
                        return true;
                    }
                }
            }

            if (skipConditions.NearPosition is { } nearPosition &&
                clientState.TerritoryType == nearPosition.TerritoryId)
            {
                if (Vector3.Distance(nearPosition.Position, clientState.LocalPlayer!.Position) <=
                    nearPosition.MaximumDistance)
                {
                    logger.LogInformation("Skipping step, as we're near the position");
                    return true;
                }
            }

            if (skipConditions.ExtraCondition != null && skipConditions.ExtraCondition != EExtraSkipCondition.None)
            {
                var position = clientState.LocalPlayer?.Position;
                if (position != null &&
                    clientState.TerritoryType != 0 &&
                    MatchesExtraCondition(skipConditions.ExtraCondition.Value, position.Value, clientState.TerritoryType))
                {
                    logger.LogInformation("Skipping step, extra condition {} matches", skipConditions.ExtraCondition);
                    return true;
                }
            }

            if (step.PickUpQuestId != null && questFunctions.IsQuestAcceptedOrComplete(step.PickUpQuestId))
            {
                logger.LogInformation("Skipping step, as we have already picked up the relevant quest");
                return true;
            }

            if (step.TurnInQuestId != null && questFunctions.IsQuestComplete(step.TurnInQuestId))
            {
                logger.LogInformation("Skipping step, as we have already completed the relevant quest");
                return true;
            }

            return false;
        }

        private static bool MatchesExtraCondition(EExtraSkipCondition condition, Vector3 position, ushort territoryType)
        {
            return condition switch
            {
                EExtraSkipCondition.WakingSandsMainArea => territoryType == 212 && position.X < 24,
                EExtraSkipCondition.RisingStonesSolar => territoryType == 351 && position.Z <= -28,
                EExtraSkipCondition.RoguesGuild => territoryType == 129 && position.Y <= -115,
                EExtraSkipCondition.DockStorehouse => territoryType == 137 && position.Y <= -20,
                _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null)
            };
        }

        public override ETaskResult Update() => ETaskResult.SkipRemainingTasksForStep;
    }
}
