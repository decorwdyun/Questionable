using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Steps.Shared;
using Questionable.Controller.Utils;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using AethernetShortcut = Questionable.Controller.Steps.Shared.AethernetShortcut;

namespace Questionable.Controller.Steps.Interactions;

internal static class UseItem
{
    public const int VesperBayAetheryteTicket = 30362;

    internal sealed class Factory(
        Mount.Factory mountFactory,
        MoveTo.Factory moveFactory,
        Interact.Factory interactFactory,
        AetheryteShortcut.Factory aetheryteShortcutFactory,
        AethernetShortcut.Factory aethernetShortcutFactory,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        IClientState clientState,
        TerritoryData territoryData,
        ILoggerFactory loggerFactory,
        ILogger<Factory> logger)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.UseItem)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);

            if (step.ItemId == VesperBayAetheryteTicket)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(step.ItemId.Value) == 0)
                        return CreateVesperBayFallbackTask();
                }

                var task = OnSelf(quest.Id, step.ItemId.Value, step.CompletionQuestVariablesFlags);

                int currentStepIndex = sequence.Steps.IndexOf(step);
                QuestStep? nextStep = sequence.Steps.Skip(currentStepIndex + 1).FirstOrDefault();
                Vector3? nextPosition = (nextStep ?? step).Position;
                return
                [
                    task,
                    new WaitConditionTask(() => clientState.TerritoryType == 140,
                        $"Wait(territory: {territoryData.GetNameAndId(140)})"),
                    mountFactory.Mount(140,
                        nextPosition != null ? Mount.EMountIf.AwayFromPosition : Mount.EMountIf.Always,
                        nextPosition),
                    moveFactory.Move(new MoveTo.MoveParams(140, new(-408.92343f, 23.167036f, -351.16223f), 0.25f,
                        DataId: null, DisableNavMesh: true, Sprint: false, Fly: false))
                ];
            }

            var unmount = mountFactory.Unmount();
            if (step.GroundTarget == true)
            {
                ITask task;
                if (step.DataId != null)
                    task = OnGroundTarget(quest.Id, step.DataId.Value, step.ItemId.Value,
                        step.CompletionQuestVariablesFlags);
                else
                {
                    ArgumentNullException.ThrowIfNull(step.Position);
                    task = OnPosition(quest.Id, step.Position.Value, step.ItemId.Value,
                        step.CompletionQuestVariablesFlags);
                }

                return [unmount, task];
            }
            else if (step.DataId != null)
            {
                var task = OnObject(quest.Id, step.DataId.Value, step.ItemId.Value, step.CompletionQuestVariablesFlags);
                return [unmount, task];
            }
            else
            {
                var task = OnSelf(quest.Id, step.ItemId.Value, step.CompletionQuestVariablesFlags);
                return [unmount, task];
            }
        }

        public ITask OnGroundTarget(ElementId questId, uint dataId, uint itemId,
            List<QuestWorkValue?> completionQuestVariablesFlags)
        {
            return new UseOnGround(questId, dataId, itemId, completionQuestVariablesFlags, gameFunctions,
                questFunctions, condition, loggerFactory.CreateLogger<UseOnGround>());
        }

        public ITask OnPosition(ElementId questId, Vector3 position, uint itemId,
            List<QuestWorkValue?> completionQuestVariablesFlags)
        {
            return new UseOnPosition(questId, position, itemId, completionQuestVariablesFlags, gameFunctions,
                questFunctions, condition, loggerFactory.CreateLogger<UseOnPosition>());
        }

        public ITask OnObject(ElementId questId, uint dataId, uint itemId,
            List<QuestWorkValue?> completionQuestVariablesFlags, bool startingCombat = false)
        {
            return new UseOnObject(questId, dataId, itemId, completionQuestVariablesFlags, startingCombat,
                questFunctions, gameFunctions, condition, loggerFactory.CreateLogger<UseOnObject>());
        }

        public ITask OnSelf(ElementId questId, uint itemId, List<QuestWorkValue?> completionQuestVariablesFlags)
        {
            return new Use(questId, itemId, completionQuestVariablesFlags, gameFunctions, questFunctions, condition,
                loggerFactory.CreateLogger<Use>());
        }

        private IEnumerable<ITask> CreateVesperBayFallbackTask()
        {
            logger.LogWarning("No vesper bay aetheryte tickets in inventory, navigating via ferry in Limsa instead");

            uint npcId = 1003540;
            ushort territoryId = 129;
            Vector3 destination = new(-360.9217f, 8f, 38.92566f);
            yield return aetheryteShortcutFactory.Use(null, null, EAetheryteLocation.Limsa, territoryId);
            yield return aethernetShortcutFactory.Use(EAetheryteLocation.Limsa, EAetheryteLocation.LimsaArcanist);
            yield return new WaitAtEnd.WaitDelay();
            yield return
                moveFactory.Move(new MoveTo.MoveParams(territoryId, destination, DataId: npcId, Sprint: false));
            yield return interactFactory.Interact(npcId, null, EInteractionType.None, true);
        }
    }

    private abstract class UseItemBase(
        ElementId? questId,
        uint itemId,
        IList<QuestWorkValue?> completionQuestVariablesFlags,
        bool startingCombat,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger logger) : ITask
    {
        private bool _usedItem;
        private DateTime _continueAt;
        private int _itemCount;

        public ElementId? QuestId => questId;
        public uint ItemId => itemId;
        public IList<QuestWorkValue?> CompletionQuestVariablesFlags => completionQuestVariablesFlags;
        public bool StartingCombat => startingCombat;

        protected abstract bool UseItem();

        public unsafe bool Start()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                throw new TaskException("No InventoryManager");

            _itemCount = inventoryManager->GetInventoryItemCount(ItemId);
            if (_itemCount == 0)
                throw new TaskException($"Don't have any {ItemId} in inventory (checks NQ only)");

            _usedItem = UseItem();
            _continueAt = DateTime.Now.Add(GetRetryDelay());
            return true;
        }

        public unsafe ETaskResult Update()
        {
            if (QuestId is QuestId realQuestId && QuestWorkUtils.HasCompletionFlags(CompletionQuestVariablesFlags))
            {
                QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(realQuestId);
                if (questWork != null &&
                    QuestWorkUtils.MatchesQuestWork(CompletionQuestVariablesFlags, questWork))
                    return ETaskResult.TaskComplete;
            }

            if (DateTime.Now <= _continueAt)
                return ETaskResult.StillRunning;

            if (StartingCombat && condition[ConditionFlag.InCombat])
                return ETaskResult.TaskComplete;

            if (ItemId == VesperBayAetheryteTicket && _usedItem)
            {
                InventoryManager* inventoryManager = InventoryManager.Instance();
                if (inventoryManager == null)
                {
                    logger.LogWarning("InventoryManager is not available");
                    return ETaskResult.StillRunning;
                }

                int itemCount = inventoryManager->GetInventoryItemCount(ItemId);
                if (itemCount == _itemCount)
                {
                    // TODO Better handling for game-provided errors, i.e. reacting to the 'Could not use' messages. UseItem() is successful in this case (and returns 0)
                    logger.LogInformation(
                        "Attempted to use vesper bay aetheryte ticket, but it didn't consume an item - reattempting next frame");
                    _usedItem = false;
                    return ETaskResult.StillRunning;
                }
            }

            if (!_usedItem)
            {
                _usedItem = UseItem();
                _continueAt = DateTime.Now.Add(GetRetryDelay());
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        private TimeSpan GetRetryDelay()
        {
            if (ItemId == VesperBayAetheryteTicket)
                return TimeSpan.FromSeconds(11);
            else
                return TimeSpan.FromSeconds(5);
        }
    }


    private sealed class UseOnGround(
        ElementId? questId,
        uint dataId,
        uint itemId,
        IList<QuestWorkValue?> completionQuestVariablesFlags,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<UseOnGround> logger)
        : UseItemBase(questId, itemId, completionQuestVariablesFlags, false, questFunctions, condition, logger)
    {
        protected override bool UseItem() => gameFunctions.UseItemOnGround(dataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on ground at {dataId})";
    }

    private sealed class UseOnPosition(
        ElementId? questId,
        Vector3 position,
        uint itemId,
        IList<QuestWorkValue?> completionQuestVariablesFlags,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<UseOnPosition> logger)
        : UseItemBase(questId, itemId, completionQuestVariablesFlags, false, questFunctions, condition, logger)
    {
        protected override bool UseItem() => gameFunctions.UseItemOnPosition(position, ItemId);

        public override string ToString() =>
            $"UseItem({ItemId} on ground at {position.ToString("G", CultureInfo.InvariantCulture)})";
    }

    private sealed class UseOnObject(
        ElementId? questId,
        uint dataId,
        uint itemId,
        IList<QuestWorkValue?> completionQuestVariablesFlags,
        bool startingCombat,
        QuestFunctions questFunctions,
        GameFunctions gameFunctions,
        ICondition condition,
        ILogger<UseOnObject> logger)
        : UseItemBase(questId, itemId, completionQuestVariablesFlags, startingCombat, questFunctions, condition, logger)
    {
        protected override bool UseItem() => gameFunctions.UseItem(dataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on {dataId})";
    }

    private sealed class Use(
        ElementId? questId,
        uint itemId,
        IList<QuestWorkValue?> completionQuestVariablesFlags,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<Use> logger)
        : UseItemBase(questId, itemId, completionQuestVariablesFlags, false, questFunctions, condition, logger)
    {
        protected override bool UseItem() => gameFunctions.UseItem(ItemId);

        public override string ToString() => $"UseItem({ItemId})";
    }
}
