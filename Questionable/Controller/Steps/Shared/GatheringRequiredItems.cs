using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LLib.GameData;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.GatheringPaths;
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
        AetheryteData aetheryteData) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            foreach (var requiredGatheredItems in step.RequiredGatheredItems)
            {
                EClassJob currentClassJob = (EClassJob)clientState.LocalPlayer!.ClassJob.Id;
                EClassJob classJob = currentClassJob;
                if (requiredGatheredItems.ClassJob != null)
                    classJob = (EClassJob)requiredGatheredItems.ClassJob.Value;

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

                if (gatheringRoot.AetheryteShortcut != null && clientState.TerritoryType != gatheringRoot.TerritoryId)
                {
                    ushort expectedTerritoryId = gatheringRoot.TerritoryId;
                    if (gatheringRoot.AethernetShortcut != null)
                        expectedTerritoryId = aetheryteData.TerritoryIds[gatheringRoot.AethernetShortcut.From];

                    yield return serviceProvider.GetRequiredService<AetheryteShortcut.UseAetheryteShortcut>()
                        .With(null, gatheringRoot.AetheryteShortcut.Value, expectedTerritoryId);
                }

                if (gatheringRoot.AethernetShortcut != null)
                {
                    yield return serviceProvider.GetRequiredService<AethernetShortcut.UseAethernetShortcut>()
                        .With(gatheringRoot.AethernetShortcut.From, gatheringRoot.AethernetShortcut.To);
                }

                yield return new WaitConditionTask(() => clientState.TerritoryType == gatheringRoot.TerritoryId,
                    $"Wait(territory: {territoryData.GetNameAndId(gatheringRoot.TerritoryId)})");

                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

                yield return serviceProvider.GetRequiredService<StartGathering>()
                    .With(gatheringPointId, requiredGatheredItems);
            }
        }

        private unsafe bool HasRequiredItems(GatheredItem requiredGatheredItems)
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager != null &&
                   inventoryManager->GetInventoryItemCount(requiredGatheredItems.ItemId,
                       minCollectability: (short)requiredGatheredItems.Collectability) >=
                   requiredGatheredItems.ItemCount;
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new NotImplementedException();
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
