using System;
using System.Linq;
using Dalamud.Plugin.Services;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Questionable.External;
using Questionable.Model.Questing;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Shared;

internal static class Craft
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Craft)
                return null;

            ArgumentNullException.ThrowIfNull(step.ItemId);
            ArgumentNullException.ThrowIfNull(step.ItemCount);
            return serviceProvider.GetRequiredService<DoCraft>()
                .With(step.ItemId.Value, step.ItemCount.Value);
        }
    }

    internal sealed class DoCraft(IDataManager dataManager, IClientState clientState, ArtisanIpc artisanIpc) : ITask
    {
        private uint _itemId;
        private int _itemCount;

        public ITask With(uint itemId, int itemCount)
        {
            _itemId = itemId;
            _itemCount = itemCount;
            return this;
        }

        public bool Start()
        {
            RecipeLookup? recipeLookup = dataManager.GetExcelSheet<RecipeLookup>()!.GetRow(_itemId);
            if (recipeLookup == null)
                throw new TaskException($"Item {_itemId} is not craftable");

            uint recipeId = ((EClassJob)clientState.LocalPlayer!.ClassJob.Id) switch
            {
                EClassJob.Carpenter => recipeLookup.CRP.Row,
                EClassJob.Blacksmith => recipeLookup.BSM.Row,
                EClassJob.Armorer => recipeLookup.ARM.Row,
                EClassJob.Goldsmith => recipeLookup.GSM.Row,
                EClassJob.Leatherworker => recipeLookup.LTW.Row,
                EClassJob.Weaver => recipeLookup.WVR.Row,
                EClassJob.Alchemist => recipeLookup.ALC.Row,
                EClassJob.Culinarian => recipeLookup.CUL.Row,
                _ => 0
            };

            if (recipeId == 0)
            {
                recipeId = new[]{
                        recipeLookup.CRP.Row,
                        recipeLookup.BSM.Row,
                        recipeLookup.ARM.Row,
                        recipeLookup.GSM.Row,
                        recipeLookup.LTW.Row,
                        recipeLookup.WVR.Row,
                        recipeLookup.ALC.Row,
                        recipeLookup.WVR.Row
                    }
                    .FirstOrDefault(x => x != 0);
            }

            if (recipeId == 0)
                throw new TaskException($"Unable to determine recipe for item {_itemId}");

            if (!artisanIpc.CraftItem((ushort)recipeId, _itemCount))
                throw new TaskException($"Failed to start Artisan craft for recipe {recipeId}");

            return true;
        }

        public ETaskResult Update()
        {
            return ETaskResult.StillRunning;
        }
    }
}
