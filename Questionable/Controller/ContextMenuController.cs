using System;
using System.Linq;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LLib.GameData;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Functions;
using Questionable.GameStructs;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller;

internal sealed class ContextMenuController : IDisposable
{
    private readonly IContextMenu _contextMenu;
    private readonly QuestController _questController;
    private readonly GatheringData _gatheringData;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestFunctions _questFunctions;
    private readonly IGameGui _gameGui;
    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;
    private readonly ILogger<ContextMenuController> _logger;

    public ContextMenuController(
        IContextMenu contextMenu,
        QuestController questController,
        GatheringData gatheringData,
        QuestRegistry questRegistry,
        QuestData questData,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        IGameGui gameGui,
        IChatGui chatGui,
        IClientState clientState,
        ILogger<ContextMenuController> logger)
    {
        _contextMenu = contextMenu;
        _questController = questController;
        _gatheringData = gatheringData;
        _questRegistry = questRegistry;
        _questData = questData;
        _gameFunctions = gameFunctions;
        _questFunctions = questFunctions;
        _gameGui = gameGui;
        _chatGui = chatGui;
        _clientState = clientState;
        _logger = logger;

        _contextMenu.OnMenuOpened += MenuOpened;
    }

    private void MenuOpened(IMenuOpenedArgs args)
    {
        uint itemId = (uint)_gameGui.HoveredItem;
        if (itemId == 0)
            return;

        if (itemId > 1_000_000)
            itemId -= 1_000_000;

        if (itemId >= 500_000)
            itemId -= 500_000;

        if (_gatheringData.TryGetCustomDeliveryNpc(itemId, out uint npcId))
        {
            AddContextMenuEntry(args, itemId, npcId, EClassJob.Miner, "Mine");
            AddContextMenuEntry(args, itemId, npcId, EClassJob.Botanist, "Harvest");
        }
    }

    private void AddContextMenuEntry(IMenuOpenedArgs args, uint itemId, uint npcId, EClassJob classJob, string verb)
    {
        EClassJob currentClassJob = (EClassJob)_clientState.LocalPlayer!.ClassJob.Id;
        if (classJob != currentClassJob && currentClassJob is EClassJob.Miner or EClassJob.Botanist)
            return;

        if (!_gatheringData.TryGetGatheringPointId(itemId, classJob, out _))
        {
            _logger.LogInformation("No gathering point found for current job.");
            return;
        }

        ushort collectability = _gatheringData.GetRecommendedCollectability(itemId);
        int quantityToGather = collectability > 0 ? 6 : int.MaxValue;
        if (collectability == 0)
            return;

        unsafe
        {
            var agentSatisfactionSupply = AgentSatisfactionSupply.Instance();
            if (agentSatisfactionSupply->IsAgentActive())
            {
                int maxTurnIns = agentSatisfactionSupply->NpcInfo.SatisfactionRank == 1 ? 3 : 6;
                quantityToGather = Math.Min(agentSatisfactionSupply->RemainingAllowances,
                    ((AgentSatisfactionSupply2*)agentSatisfactionSupply)->CalculateTurnInsToNextRank(maxTurnIns));
            }
        }

        string lockedReasonn = string.Empty;
        if (!_questFunctions.IsClassJobUnlocked(classJob))
            lockedReasonn = $"{classJob} not unlocked";
        else if (quantityToGather == 0)
            lockedReasonn = "No allowances";
        else if (quantityToGather > _gameFunctions.GetFreeInventorySlots())
            lockedReasonn = "Inventory full";
        else if (_gameFunctions.IsOccupied())
            lockedReasonn = "Can't be used while interacting";

        string name = $"{verb} with Questionable";
        if (!string.IsNullOrEmpty(lockedReasonn))
            name += $" ({lockedReasonn})";

        args.AddMenuItem(new MenuItem
        {
            Prefix = SeIconChar.Hyadelyn,
            PrefixColor = 52,
            Name = name,
            OnClicked = _ => StartGathering(npcId, itemId, quantityToGather, collectability, classJob),
            IsEnabled = string.IsNullOrEmpty(lockedReasonn),
        });
    }

    private void StartGathering(uint npcId, uint itemId, int quantity, ushort collectability, EClassJob classJob)
    {
        var info = (SatisfactionSupplyInfo)_questData.GetAllByIssuerDataId(npcId)
            .Single(x => x is SatisfactionSupplyInfo);
        if (_questRegistry.TryGetQuest(info.QuestId, out Quest? quest))
        {
            var step = quest.FindSequence(0)!.FindStep(0)!;
            step.RequiredGatheredItems =
            [
                new GatheredItem
                {
                    ItemId = itemId,
                    ItemCount = quantity,
                    Collectability = collectability,
                    ClassJob = (uint)classJob,
                }
            ];
            _questController.SetGatheringQuest(quest);
            _questController.ExecuteNextStep(QuestController.EAutomationType.CurrentQuestOnly);
        }
        else
            _chatGui.PrintError($"No associated quest ({info.QuestId}).", "Questionable");
    }

    public void Dispose()
    {
        _contextMenu.OnMenuOpened -= MenuOpened;
    }
}
