using System;
using System.Linq;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.Logging;
using Questionable.Data;
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
        _gameGui = gameGui;
        _chatGui = chatGui;
        _clientState = clientState;
        _logger = logger;

        _contextMenu.OnMenuOpened += MenuOpened;
    }

    private void MenuOpened(IMenuOpenedArgs args)
    {
        uint itemId = (uint) _gameGui.HoveredItem;
        if (itemId == 0)
            return;

        if (itemId > 1_000_000)
            itemId -= 1_000_000;

        if (itemId >= 500_000)
            itemId -= 500_000;

        if (!_gatheringData.TryGetGatheringPointId(itemId, _clientState.LocalPlayer!.ClassJob.Id, out _))
        {
            _logger.LogInformation("No gathering point found for current job.");
            return;
        }

        if (_gatheringData.TryGetCustomDeliveryNpc(itemId, out uint npcId))
        {
            ushort collectability = _gatheringData.GetRecommendedCollectability(itemId);
            int quantityToGather = collectability > 0 ? 6 : int.MaxValue;
            if (collectability == 0)
                return;

            args.AddMenuItem(new MenuItem
            {
                Prefix = SeIconChar.Hyadelyn,
                PrefixColor = 52,
                Name = "Gather with Questionable",
                OnClicked = _ => StartGathering(npcId, itemId, quantityToGather, collectability),
            });
        }
    }

    private void StartGathering(uint npcId, uint itemId, int quantity, ushort collectability)
    {
        var info = (SatisfactionSupplyInfo)_questData.GetAllByIssuerDataId(npcId).Single(x => x is SatisfactionSupplyInfo);
        if (_questRegistry.TryGetQuest(info.QuestId, out Quest? quest))
        {
            var step = quest.FindSequence(0)!.FindStep(0)!;
            step.RequiredGatheredItems =
            [
                new GatheredItem
                {
                    ItemId = itemId,
                    ItemCount = quantity,
                    Collectability = collectability
                }
            ];
            _questController.SetNextQuest(quest);
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
