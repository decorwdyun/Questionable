using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
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
        GatheringController gatheringController,
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
                EClassJob classJob = currentClassJob;
                if (itemToGather.QuestAcceptedAsClass != null)
                {
                    classJob = (EClassJob)itemToGather.QuestAcceptedAsClass.Value;
                    if (!IsClassJobQuestWasAcceptedWith(quest.Id, classJob))
                        continue;
                }

                if (!gatheringData.TryGetGatheringPointId(itemToGather.ItemId, classJob,
                        out GatheringPointId? gatheringPointId))
                    throw new TaskException($"No gathering point found for item {itemToGather.ItemId}");

                if (!gatheringPointRegistry.TryGetGatheringPoint(gatheringPointId, out GatheringRoot? gatheringRoot))
                    throw new TaskException($"No path found for gathering point {gatheringPointId}");

                if (classJob != currentClassJob)
                {
                    yield return new SwitchClassJob(classJob, clientState);
                }

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
                                yield return CreateSkipMarkerTask();
                            else
                                yield return task;
                    }
                }

                ushort territoryId = gatheringRoot.Steps.Last().TerritoryId;
                yield return new WaitConditionTask(() => clientState.TerritoryType == territoryId,
                    $"Wait(territory: {territoryData.GetNameAndId(territoryId)})");

                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

                yield return CreateStartGatheringTask(gatheringPointId, itemToGather);
                yield return new WaitAtEnd.WaitDelay();
            }
        }

        private unsafe bool IsClassJobQuestWasAcceptedWith(ElementId questId, EClassJob expectedClassJob)
        {
            if (questId is not QuestId)
                return true;

            QuestWork* questWork = QuestManager.Instance()->GetQuestById(questId.Value);
            if (questWork->AcceptClassJob != 0)
                return (EClassJob)questWork->AcceptClassJob == expectedClassJob;

            return true;
        }

        private unsafe bool HasRequiredItems(GatheredItem itemToGather)
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager != null &&
                   inventoryManager->GetInventoryItemCount(itemToGather.ItemId,
                       minCollectability: (short)itemToGather.Collectability) >=
                   itemToGather.ItemCount;
        }

        private StartGathering CreateStartGatheringTask(GatheringPointId gatheringPointId, GatheredItem gatheredItem)
        {
            return new StartGathering(gatheringPointId, gatheredItem, gatheringController);
        }

        private static SkipMarker CreateSkipMarkerTask()
        {
            return new SkipMarker();
        }
    }

    private sealed class StartGathering(
        GatheringPointId gatheringPointId,
        GatheredItem gatheredItem,
        GatheringController gatheringController) : ITask
    {
        public bool Start()
        {
            return gatheringController.Start(new GatheringController.GatheringRequest(gatheringPointId,
                gatheredItem.ItemId, gatheredItem.AlternativeItemId, gatheredItem.ItemCount,
                gatheredItem.Collectability));
        }

        public ETaskResult Update()
        {
            if (gatheringController.Update() == GatheringController.EStatus.Complete)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        public override string ToString()
        {
            if (gatheredItem.Collectability == 0)
                return $"Gather({gatheredItem.ItemCount}x {gatheredItem.ItemId})";
            else
                return
                    $"Gather({gatheredItem.ItemCount}x {gatheredItem.ItemId} {SeIconChar.Collectible.ToIconString()} {gatheredItem.Collectability})";
        }
    }

    /// <summary>
    /// A task that does nothing, but if we're skipping a step, this will be the task next in queue to be executed (instead of progressing to the next step) if gathering.
    /// </summary>
    internal sealed class SkipMarker : ITask
    {
        public bool Start() => true;
        public ETaskResult Update() => ETaskResult.TaskComplete;
        public override string ToString() => "Gather/SkipMarker";
    }
}
