using System;
using System.Linq;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LLib.GameData;
using LLib.GameUI;
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
        // no clue why this isn't the actual name, but here we are
        if (args.AddonName != null)
            return;

        uint itemId = GetHoveredSatisfactionSupplyItemId();
        if (itemId == 0)
        {
            _logger.LogTrace("Ignoring context menu, no item hovered");
            return;
        }

        if (itemId > 1_000_000)
            itemId -= 1_000_000;

        if (itemId >= 500_000)
            itemId -= 500_000;

        if (_gatheringData.TryGetCustomDeliveryNpc(itemId, out uint npcId))
        {
            AddContextMenuEntry(args, itemId, npcId, EExtendedClassJob.Miner, "Mine");
            AddContextMenuEntry(args, itemId, npcId, EExtendedClassJob.Botanist, "Harvest");
        }
        else
            _logger.LogDebug("No custom delivery NPC found for item {ItemId}.", itemId);
    }

    private unsafe uint GetHoveredSatisfactionSupplyItemId()
    {
        AgentSatisfactionSupply* agent = AgentSatisfactionSupply.Instance();
        if (agent == null || !agent->IsAgentActive())
            return 0;


        if (_gameGui.TryGetAddonByName("SatisfactionSupply", out AddonSatisfactionSupply* addon) &&
            LAddon.IsAddonReady(&addon->AtkUnitBase) &&
            addon->HoveredElementIndex is >= 0 and <= 2)
        {
            return agent->Items[addon->HoveredElementIndex].Id;
        }

        return 0;
    }

    private void AddContextMenuEntry(IMenuOpenedArgs args, uint itemId, uint npcId, EExtendedClassJob extendedClassJob,
        string verb)
    {
        EClassJob currentClassJob = (EClassJob)_clientState.LocalPlayer!.ClassJob.RowId;
        EClassJob classJob = ClassJobUtils.AsIndividualJobs(extendedClassJob).Single();
        if (classJob != currentClassJob && currentClassJob is EClassJob.Miner or EClassJob.Botanist)
            return;

        if (!_gatheringData.TryGetGatheringPointId(itemId, classJob, out _))
        {
            _logger.LogInformation("No gathering point found for {ClassJob}.", classJob);
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
                quantityToGather = Math.Min(agentSatisfactionSupply->NpcData.RemainingAllowances,
                    ((AgentSatisfactionSupply2*)agentSatisfactionSupply)->CalculateTurnInsToNextRank(maxTurnIns));
            }
        }

        string lockedReasonn = string.Empty;
        if (!_questFunctions.IsClassJobUnlocked(classJob))
            lockedReasonn = $"{classJob} 职业未解锁";
        else if (quantityToGather == 0)
            lockedReasonn = "无受理限额";
        else if (quantityToGather > _gameFunctions.GetFreeInventorySlots())
            lockedReasonn = "背包空间不足";
        else if (_gameFunctions.IsOccupied())
            lockedReasonn = "交互时无法使用";

        string name = $"{verb} with Questionable";
        if (!string.IsNullOrEmpty(lockedReasonn))
            name += $" ({lockedReasonn})";

        args.AddMenuItem(new MenuItem
        {
            Prefix = SeIconChar.Hyadelyn,
            PrefixColor = 52,
            Name = name,
            OnClicked = _ => StartGathering(npcId, itemId, quantityToGather, collectability, extendedClassJob),
            IsEnabled = string.IsNullOrEmpty(lockedReasonn),
        });
    }

    private void StartGathering(uint npcId, uint itemId, int quantity, ushort collectability,
        EExtendedClassJob extendedClassJob)
    {
        var info = (SatisfactionSupplyInfo)_questData.GetAllByIssuerDataId(npcId)
            .Single(x => x is SatisfactionSupplyInfo);
        if (_questRegistry.TryGetQuest(info.QuestId, out Quest? quest))
        {
            var sequence = quest.FindSequence(0)!;

            var switchClassStep = sequence.Steps.Single(x => x.InteractionType == EInteractionType.SwitchClass);
            switchClassStep.TargetClass = extendedClassJob;

            var gatherStep = sequence.Steps.Single(x => x.InteractionType == EInteractionType.Gather);
            gatherStep.ItemsToGather =
            [
                new GatheredItem
                {
                    ItemId = itemId,
                    ItemCount = quantity,
                    Collectability = collectability,
                }
            ];
            _questController.SetGatheringQuest(quest);
            _questController.StartGatheringQuest("SatisfactionSupply prepare gathering");
        }
        else
            _chatGui.PrintError($"No associated quest ({info.QuestId}).", "Questionable");
    }

    public void Dispose()
    {
        _contextMenu.OnMenuOpened -= MenuOpened;
    }
}
