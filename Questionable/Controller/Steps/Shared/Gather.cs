using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using LLib.GameData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Gathering;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class Gather
{
    internal sealed class Factory(
        IServiceProvider serviceProvider,
        MovementController movementController,
        GatheringPointRegistry gatheringPointRegistry,
        IClientState clientState,
        GatheringData gatheringData,
        TerritoryData territoryData,
        ILogger<Factory> logger) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Gather)
                yield break;

            foreach (var itemToGather in step.ItemsToGather)
            {
                EClassJob currentClassJob = (EClassJob)clientState.LocalPlayer!.ClassJob.Id;
                if (!gatheringData.TryGetGatheringPointId(itemToGather.ItemId, currentClassJob,
                        out GatheringPointId? gatheringPointId))
                    throw new TaskException($"No gathering point found for item {itemToGather.ItemId}");

                if (!gatheringPointRegistry.TryGetGatheringPoint(gatheringPointId, out GatheringRoot? gatheringRoot))
                    throw new TaskException($"No path found for gathering point {gatheringPointId}");

                if (HasRequiredItems(itemToGather))
                    continue;

                using (var _ = logger.BeginScope("Gathering(inner)"))
                {
                    QuestSequence gatheringSequence = new QuestSequence
                    {
                        Sequence = 0,
                        Steps = gatheringRoot.Steps
                    };
                    foreach (var gatheringStep in gatheringSequence.Steps)
                    {
                        foreach (var task in serviceProvider.GetRequiredService<TaskCreator>()
                                     .CreateTasks(quest, gatheringSequence, gatheringStep))
                            if (task is WaitAtEnd.NextStep)
                                yield return new SkipMarker();
                            else
                                yield return task;
                    }
                }

                ushort territoryId = gatheringRoot.Steps.Last().TerritoryId;
                yield return new WaitCondition.Task(() => clientState.TerritoryType == territoryId,
                    $"Wait(territory: {territoryData.GetNameAndId(territoryId)})");

                yield return new WaitCondition.Task(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

                yield return new GatheringTask(gatheringPointId, itemToGather);
                yield return new WaitAtEnd.WaitDelay();
            }
        }

        private unsafe bool HasRequiredItems(GatheredItem itemToGather)
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager != null &&
                   inventoryManager->GetInventoryItemCount(itemToGather.ItemId,
                       minCollectability: (short)itemToGather.Collectability) >=
                   itemToGather.ItemCount;
        }
    }

    internal sealed record GatheringTask(
        GatheringPointId gatheringPointId,
        GatheredItem gatheredItem) : ITask
    {
        public override string ToString()
        {
            if (gatheredItem.Collectability == 0)
                return $"Gather({gatheredItem.ItemCount}x {gatheredItem.ItemId})";
            else
                return
                    $"Gather({gatheredItem.ItemCount}x {gatheredItem.ItemId} {SeIconChar.Collectible.ToIconString()} {gatheredItem.Collectability})";
        }
    }

    internal sealed class StartGathering(GatheringController gatheringController) : TaskExecutor<GatheringTask>,
        IToastAware
    {
        protected override bool Start()
        {
            return gatheringController.Start(new GatheringController.GatheringRequest(Task.gatheringPointId,
                Task.gatheredItem.ItemId, Task.gatheredItem.AlternativeItemId, Task.gatheredItem.ItemCount,
                Task.gatheredItem.Collectability));
        }

        public override ETaskResult Update()
        {
            if (gatheringController.Update() == GatheringController.EStatus.Complete)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        public bool OnErrorToast(SeString message)
        {
            bool isHandled = false;
            gatheringController.OnErrorToast(ref message, ref isHandled);
            return isHandled;
        }
    }

    /// <summary>
    /// A task that does nothing, but if we're skipping a step, this will be the task next in queue to be executed (instead of progressing to the next step) if gathering.
    /// </summary>
    internal sealed class SkipMarker : ITask
    {
        public override string ToString() => "Gather/SkipMarker";
    }

    internal sealed class DoSkip : TaskExecutor<SkipMarker>
    {
        protected override bool Start() => true;
        public override ETaskResult Update() => ETaskResult.TaskComplete;
    }
}
