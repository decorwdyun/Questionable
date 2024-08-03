using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
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
        IClientState clientState,
        GatheringData gatheringData) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            foreach (var requiredGatheredItems in step.RequiredGatheredItems)
            {
                if (!gatheringData.TryGetGatheringPointId(requiredGatheredItems.ItemId,
                        clientState.LocalPlayer!.ClassJob.Id, out var gatheringPointId))
                    throw new TaskException($"No gathering point found for item {requiredGatheredItems.ItemId}");

                if (!AssemblyGatheringLocationLoader.GetLocations()
                        .TryGetValue(gatheringPointId, out GatheringRoot? gatheringRoot))
                    throw new TaskException("No path found for gathering point");

                if (gatheringRoot.AetheryteShortcut != null && clientState.TerritoryType != gatheringRoot.TerritoryId)
                {
                    yield return serviceProvider.GetRequiredService<AetheryteShortcut.UseAetheryteShortcut>()
                        .With(null, gatheringRoot.AetheryteShortcut.Value, gatheringRoot.TerritoryId);
                }

                yield return serviceProvider.GetRequiredService<StartGathering>()
                    .With(gatheringPointId, requiredGatheredItems);
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new NotImplementedException();
    }

    internal sealed class StartGathering(GatheringController gatheringController) : ITask
    {
        private ushort _gatheringPointId;
        private GatheredItem _gatheredItem = null!;

        public ITask With(ushort gatheringPointId, GatheredItem gatheredItem)
        {
            _gatheringPointId = gatheringPointId;
            _gatheredItem = gatheredItem;
            return this;
        }

        public bool Start()
        {
            return gatheringController.Start(new GatheringController.GatheringRequest(_gatheringPointId,
                _gatheredItem.ItemId, _gatheredItem.ItemCount, _gatheredItem.Collectability));
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
                return $"Gather({_gatheredItem.ItemCount}x {_gatheredItem.ItemId} {SeIconChar.Collectible.ToIconString()} {_gatheredItem.Collectability})";
        }
    }
}
