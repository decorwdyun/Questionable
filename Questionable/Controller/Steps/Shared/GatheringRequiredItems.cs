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
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Gathering;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class GatheringRequiredItems
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
            foreach (var requiredGatheredItems in step.RequiredGatheredItems)
            {
                EClassJob currentClassJob = (EClassJob)clientState.LocalPlayer!.ClassJob.Id;
                EClassJob classJob = currentClassJob;
                if (requiredGatheredItems.QuestAcceptedAsClass != null)
                {
                    classJob = (EClassJob)requiredGatheredItems.QuestAcceptedAsClass.Value;
                    if (!IsClassJobQuestWasAcceptedWith(quest.Id, classJob))
                        continue;
                }

                if (!gatheringData.TryGetGatheringPointId(requiredGatheredItems.ItemId, classJob,
                        out GatheringPointId? gatheringPointId))
                    throw new TaskException($"No gathering point found for item {requiredGatheredItems.ItemId}");

                if (!gatheringPointRegistry.TryGetGatheringPoint(gatheringPointId, out GatheringRoot? gatheringRoot))
                    throw new TaskException($"No path found for gathering point {gatheringPointId}");

                if (classJob != currentClassJob)
                {
                    yield return serviceProvider.GetRequiredService<SwitchClassJob>()
                        .With(classJob);
                }

                if (HasRequiredItems(requiredGatheredItems))
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
                            if (task is not WaitAtEnd.NextStep)
                                yield return task;
                    }
                }

                ushort territoryId = gatheringRoot.Steps.Last().TerritoryId;
                yield return new WaitConditionTask(() => clientState.TerritoryType == territoryId,
                    $"Wait(territory: {territoryData.GetNameAndId(territoryId)})");

                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

                yield return serviceProvider.GetRequiredService<StartGathering>()
                    .With(gatheringPointId, requiredGatheredItems);
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

        private unsafe bool HasRequiredItems(GatheredItem requiredGatheredItems)
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager != null &&
                   inventoryManager->GetInventoryItemCount(requiredGatheredItems.ItemId,
                       minCollectability: (short)requiredGatheredItems.Collectability) >=
                   requiredGatheredItems.ItemCount;
        }
    }

    internal sealed class StartGathering(GatheringController gatheringController) : ITask
    {
        private GatheringPointId _gatheringPointId = null!;
        private GatheredItem _gatheredItem = null!;

        public ITask With(GatheringPointId gatheringPointId, GatheredItem gatheredItem)
        {
            _gatheringPointId = gatheringPointId;
            _gatheredItem = gatheredItem;
            return this;
        }

        public bool Start()
        {
            return gatheringController.Start(new GatheringController.GatheringRequest(_gatheringPointId,
                _gatheredItem.ItemId, _gatheredItem.AlternativeItemId, _gatheredItem.ItemCount,
                _gatheredItem.Collectability));
        }

        public ETaskResult Update()
        {
            if (gatheringController.Update() == GatheringController.EStatus.Complete)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        public override string ToString()
        {
            if (_gatheredItem.Collectability == 0)
                return $"Gather({_gatheredItem.ItemCount}x {_gatheredItem.ItemId})";
            else
                return
                    $"Gather({_gatheredItem.ItemCount}x {_gatheredItem.ItemId} {SeIconChar.Collectible.ToIconString()} {_gatheredItem.Collectability})";
        }
    }
}
