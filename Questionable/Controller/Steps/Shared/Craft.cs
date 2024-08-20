using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Logging;
using Questionable.External;
using Questionable.Model.Questing;
using Mount = Questionable.Controller.Steps.Common.Mount;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Shared;

internal static class Craft
{
    internal sealed class Factory(
        IDataManager dataManager,
        IClientState clientState,
        ArtisanIpc artisanIpc,
        Mount.Factory mountFactory,
        ILoggerFactory loggerFactory) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Craft)
                return [];

            ArgumentNullException.ThrowIfNull(step.ItemId);
            ArgumentNullException.ThrowIfNull(step.ItemCount);
            return
            [
                mountFactory.Unmount(),
                Craft(step.ItemId.Value, step.ItemCount.Value)
            ];
        }

        public ITask Craft(uint itemId, int itemCount) =>
            new DoCraft(itemId, itemCount, dataManager, clientState, artisanIpc, loggerFactory.CreateLogger<DoCraft>());
    }

    private sealed class DoCraft(
        uint itemId,
        int itemCount,
        IDataManager dataManager,
        IClientState clientState,
        ArtisanIpc artisanIpc,
        ILogger<DoCraft> logger) : ITask
    {
        public bool Start()
        {
            if (HasRequestedItems())
            {
                logger.LogInformation("Already own {ItemCount}x {ItemId}", itemCount, itemId);
                return false;
            }

            RecipeLookup? recipeLookup = dataManager.GetExcelSheet<RecipeLookup>()!.GetRow(itemId);
            if (recipeLookup == null)
                throw new TaskException($"Item {itemId} is not craftable");

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
                throw new TaskException($"Unable to determine recipe for item {itemId}");

            int remainingItemCount = itemCount - GetOwnedItemCount();
            logger.LogInformation(
                "Starting craft for item {ItemId} with recipe {RecipeId} for {RemainingItemCount} items",
                itemId, recipeId, remainingItemCount);
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
                        addon->FireCallbackInt(-1);
                        return ETaskResult.TaskComplete;
                    }
                }
            }

            return ETaskResult.StillRunning;
        }

        private bool HasRequestedItems() => GetOwnedItemCount() >= itemCount;

        private unsafe int GetOwnedItemCount()
        {
            InventoryManager* inventoryManager = InventoryManager.Instance();
            return inventoryManager->GetInventoryItemCount(itemId, isHq: false, checkEquipped: false)
                   + inventoryManager->GetInventoryItemCount(itemId, isHq: true, checkEquipped: false);
        }

        public override string ToString() => $"Craft {itemCount}x {itemId} (with Artisan)";
    }
}
