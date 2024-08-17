using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.External;
using Questionable.Model.Questing;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Shared;

internal static class Craft
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Craft)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);
            ArgumentNullException.ThrowIfNull(step.ItemCount);
            return
            [
                serviceProvider.GetRequiredService<UnmountTask>(),
                serviceProvider.GetRequiredService<DoCraft>()
                    .With(step.ItemId.Value, step.ItemCount.Value)
            ];
        }
    }

    internal sealed class DoCraft(
        IDataManager dataManager,
        IClientState clientState,
        ArtisanIpc artisanIpc,
        ILogger<DoCraft> logger) : ITask
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
            if (HasRequestedItems())
            {
                logger.LogInformation("Already own {ItemCount}x {ItemId}", _itemCount, _itemId);
                return false;
            }

            RecipeLookup? recipeLookup = dataManager.GetExcelSheet<RecipeLookup>()!.GetRow(_itemId);
            if (recipeLookup == null)
                throw new TaskException($"Item {_itemId} is not craftable");

            uint recipeId = (EClassJob)clientState.LocalPlayer!.ClassJob.Id switch
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
                recipeId = new[]
                    {
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

            int remainingItemCount = _itemCount - GetOwnedItemCount();
            logger.LogInformation(
                "Starting craft for item {ItemId} with recipe {RecipeId} for {RemainingItemCount} items",
                _itemId, recipeId, remainingItemCount);
            if (!artisanIpc.CraftItem((ushort)recipeId, remainingItemCount))
                throw new TaskException($"Failed to start Artisan craft for recipe {recipeId}");

            return true;
        }

        public unsafe ETaskResult Update()
        {
            if (HasRequestedItems() && !artisanIpc.IsCrafting())
            {
                AgentRecipeNote* agentRecipeNote = AgentRecipeNote.Instance();
                if (agentRecipeNote != null && agentRecipeNote->IsAgentActive())
                {
                    uint addonId = agentRecipeNote->GetAddonId();
                    if (addonId == 0)
                        return ETaskResult.StillRunning;

                    AtkUnitBase* addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonById((ushort)addonId);
                    if (addon != null)
                    {
                        logger.LogInformation("Closing crafting window");
                        addon->Close(true);
                        return ETaskResult.TaskComplete;
                    }
                }
            }

            return ETaskResult.StillRunning;
        }

        private bool HasRequestedItems() => GetOwnedItemCount() >= _itemCount;

        private unsafe int GetOwnedItemCount()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager->GetInventoryItemCount(_itemId, isHq: false, checkEquipped: false)
                   + inventoryManager->GetInventoryItemCount(_itemId, isHq: true, checkEquipped: false);
        }

        public override string ToString() => $"Craft {_itemCount}x {_itemId} (with Artisan)";
    }
}
