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
        IServiceProvider serviceProvider,
        IClientState clientState,
        TerritoryData territoryData,
        ILogger<Factory> logger)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.UseItem)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            if (step.ItemId == VesperBayAetheryteTicket)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(step.ItemId.Value) == 0)
                        return CreateVesperBayFallbackTask();
                }

                var task = serviceProvider.GetRequiredService<Use>()
                    .With(quest.Id, step.ItemId.Value, step.CompletionQuestVariablesFlags);

                int currentStepIndex = sequence.Steps.IndexOf(step);
                QuestStep? nextStep = sequence.Steps.Skip(currentStepIndex + 1).SingleOrDefault();
                Vector3? nextPosition = (nextStep ?? step).Position;
                return
                [
                    unmount, task,
                    new WaitConditionTask(() => clientState.TerritoryType == 140,
                        $"Wait(territory: {territoryData.GetNameAndId(140)})"),
                    serviceProvider.GetRequiredService<MountTask>()
                        .With(140,
                            nextPosition != null ? MountTask.EMountIf.AwayFromPosition : MountTask.EMountIf.Always,
                            nextPosition),
                    serviceProvider.GetRequiredService<Move.MoveInternal>()
                        .With(140, new(-408.92343f, 23.167036f, -351.16223f), 0.25f, dataId: null, disableNavMesh: true,
                            sprint: false, fly: false)
                ];
            }

            if (step.GroundTarget == true)
            {
                ITask task;
                if (step.DataId != null)
                    task = serviceProvider.GetRequiredService<UseOnGround>()
                        .With(quest.Id, step.DataId.Value, step.ItemId.Value, step.CompletionQuestVariablesFlags);
                else
                {
                    ArgumentNullException.ThrowIfNull(step.Position);
                    task = serviceProvider.GetRequiredService<UseOnPosition>()
                        .With(quest.Id, step.Position.Value, step.ItemId.Value,
                            step.CompletionQuestVariablesFlags);
                }

                return [unmount, task];
            }
            else if (step.DataId != null)
            {
                var task = serviceProvider.GetRequiredService<UseOnObject>()
                    .With(quest.Id, step.DataId.Value, step.ItemId.Value, step.CompletionQuestVariablesFlags);
                return [unmount, task];
            }
            else
            {
                var task = serviceProvider.GetRequiredService<Use>()
                    .With(quest.Id, step.ItemId.Value, step.CompletionQuestVariablesFlags);
                return [unmount, task];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();

        private IEnumerable<ITask> CreateVesperBayFallbackTask()
        {
            logger.LogWarning("No vesper bay aetheryte tickets in inventory, navigating via ferry in Limsa instead");

            uint npcId = 1003540;
            ushort territoryId = 129;
            Vector3 destination = new(-360.9217f, 8f, 38.92566f);
            yield return serviceProvider.GetRequiredService<AetheryteShortcut.UseAetheryteShortcut>()
                .With(null, EAetheryteLocation.Limsa, territoryId);
            yield return serviceProvider.GetRequiredService<AethernetShortcut.UseAethernetShortcut>()
                .With(EAetheryteLocation.Limsa, EAetheryteLocation.LimsaArcanist);
            yield return serviceProvider.GetRequiredService<WaitAtEnd.WaitDelay>();
            yield return serviceProvider.GetRequiredService<Move.MoveInternal>()
                .With(territoryId, destination, dataId: npcId, sprint: false);
            yield return serviceProvider.GetRequiredService<Interact.DoInteract>()
                .With(npcId, null, EInteractionType.None, true);
        }
    }

    internal abstract class UseItemBase(QuestFunctions questFunctions, ICondition condition, ILogger logger) : ITask
    {
        private bool _usedItem;
        private DateTime _continueAt;
        private int _itemCount;

        public ElementId? QuestId { get; set; }
        public uint ItemId { get; set; }
        public IList<QuestWorkValue?> CompletionQuestVariablesFlags { get; set; } = new List<QuestWorkValue?>();
        public bool StartingCombat { get; set; }

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
            if (QuestId is QuestId questId && QuestWorkUtils.HasCompletionFlags(CompletionQuestVariablesFlags))
            {
                QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(questId);
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


    internal sealed class UseOnGround(
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<UseOnGround> logger)
        : UseItemBase(questFunctions, condition, logger)
    {
        public uint DataId { get; set; }

        public ITask With(ElementId? questId, uint dataId, uint itemId,
            IList<QuestWorkValue?> completionQuestVariablesFlags)
        {
            QuestId = questId;
            DataId = dataId;
            ItemId = itemId;
            CompletionQuestVariablesFlags = completionQuestVariablesFlags;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItemOnGround(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on ground at {DataId})";
    }

    internal sealed class UseOnPosition(
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<UseOnPosition> logger)
        : UseItemBase(questFunctions, condition, logger)
    {
        public Vector3 Position { get; set; }

        public ITask With(ElementId? questId, Vector3 position, uint itemId,
            IList<QuestWorkValue?> completionQuestVariablesFlags)
        {
            QuestId = questId;
            Position = position;
            ItemId = itemId;
            CompletionQuestVariablesFlags = completionQuestVariablesFlags;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItemOnPosition(Position, ItemId);

        public override string ToString() =>
            $"UseItem({ItemId} on ground at {Position.ToString("G", CultureInfo.InvariantCulture)})";
    }

    internal sealed class UseOnObject(
        QuestFunctions questFunctions,
        GameFunctions gameFunctions,
        ICondition condition,
        ILogger<UseOnObject> logger)
        : UseItemBase(questFunctions, condition, logger)
    {
        public uint DataId { get; set; }

        public ITask With(ElementId? questId, uint dataId, uint itemId,
            IList<QuestWorkValue?> completionQuestVariablesFlags,
            bool startingCombat = false)
        {
            QuestId = questId;
            DataId = dataId;
            ItemId = itemId;
            StartingCombat = startingCombat;
            CompletionQuestVariablesFlags = completionQuestVariablesFlags;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(DataId, ItemId);

        public override string ToString() => $"UseItem({ItemId} on {DataId})";
    }

    internal sealed class Use(
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ICondition condition,
        ILogger<Use> logger)
        : UseItemBase(questFunctions, condition, logger)
    {
        public ITask With(ElementId? questId, uint itemId, IList<QuestWorkValue?> completionQuestVariablesFlags)
        {
            QuestId = questId;
            ItemId = itemId;
            CompletionQuestVariablesFlags = completionQuestVariablesFlags;
            return this;
        }

        protected override bool UseItem() => gameFunctions.UseItem(ItemId);

        public override string ToString() => $"UseItem({ItemId})";
    }
}
