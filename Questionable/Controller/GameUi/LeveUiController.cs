using System;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.GameUi;

internal sealed class LeveUiController : IDisposable
{
    private readonly QuestController _questController;
    private readonly QuestData _questData;
    private readonly QuestFunctions _questFunctions;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameGui _gameGui;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly ILogger<LeveUiController> _logger;

    public LeveUiController(QuestController questController, QuestData questData, QuestFunctions questFunctions,
        IAddonLifecycle addonLifecycle, IGameGui gameGui, ITargetManager targetManager, IFramework framework,
        ILogger<LeveUiController> logger)
    {
        _questController = questController;
        _questData = questData;
        _questFunctions = questFunctions;
        _addonLifecycle = addonLifecycle;
        _gameGui = gameGui;
        _targetManager = targetManager;
        _framework = framework;
        _logger = logger;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", JournalResultPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "GuildLeve", GuildLevePostSetup);
    }

    private bool ShouldHandleUiInteractions => _questController.IsRunning;

    private unsafe void JournalResultPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!ShouldHandleUiInteractions)
            return;

        _logger.LogInformation("Checking for quest name of journal result");
        AddonJournalResult* addon = (AddonJournalResult*)args.Addon;

        string questName = addon->AtkTextNode250->NodeText.ToString();
        if (_questController.CurrentQuest is { Quest.Id: LeveId } &&
            GameFunctions.GameStringEquals(_questController.CurrentQuest.Quest.Info.Name, questName))
        {
            _logger.LogInformation("JournalResult has the current leve, auto-accepting it");
            addon->FireCallbackInt(0);
        }
        else if (_targetManager.Target is { } target)
        {
            var issuedLeves = _questData.GetAllByIssuerDataId(target.DataId)
                .Where(x => x.QuestId is LeveId)
                .ToList();

            if (issuedLeves.Any(x => GameFunctions.GameStringEquals(x.Name, questName)))
            {
                _logger.LogInformation(
                    "JournalResult has a leve but not the one we're currently on, auto-declining it");
                addon->FireCallbackInt(1);
            }
        }
    }

    private void GuildLevePostSetup(AddonEvent type, AddonArgs args)
    {
        var target = _targetManager.Target;
        if (target == null)
            return;

        if (_questController is { IsRunning: true, NextQuest: { Quest.Id: LeveId } nextQuest } &&
            _questFunctions.IsReadyToAcceptQuest(nextQuest.Quest.Id))
        {
            /*
            var addon = (AddonGuildLeve*)args.Addon;
            var atkValues = addon->AtkValues;

            var availableLeves = _questData.GetAllByIssuerDataId(target.DataId);
            List<(int, IQuestInfo)> offeredLeves = [];
            for (int i = 0; i <= 20; ++i) // 3 leves per group, 1 label for group
            {
                string? leveName = atkValues[626 + i * 2].ReadAtkString();
                if (leveName == null)
                    continue;

                var questInfo = availableLeves.FirstOrDefault(x => GameFunctions.GameStringEquals(x.Name, leveName));
                if (questInfo == null)
                    continue;

                offeredLeves.Add((i, questInfo));

            }

            foreach (var (i, questInfo) in offeredLeves)
                _logger.LogInformation("Leve {Index} = {Id}, {Name}", i, questInfo.QuestId, questInfo.Name);
            */

            _framework.RunOnTick(() => AcceptLeveOrWait(nextQuest), TimeSpan.FromMilliseconds(100));
        }
    }

    private unsafe void AcceptLeveOrWait(QuestController.QuestProgress nextQuest, int counter = 0)
    {
        var agent = UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.LeveQuest);
        if (agent->IsAgentActive() &&
            _gameGui.TryGetAddonByName("GuildLeve", out AddonGuildLeve* addonGuildLeve) &&
            LAddon.IsAddonReady(&addonGuildLeve->AtkUnitBase) &&
            _gameGui.TryGetAddonByName("JournalDetail", out AtkUnitBase* addonJournalDetail) &&
            LAddon.IsAddonReady(addonJournalDetail))
        {
            AcceptLeve(agent, addonGuildLeve, nextQuest);
        }
        else if (counter >= 10)
            _logger.LogWarning("Unable to accept leve?");
        else
            _framework.RunOnTick(() => AcceptLeveOrWait(nextQuest, counter + 1), TimeSpan.FromMilliseconds(100));
    }

    private unsafe void AcceptLeve(AgentInterface* agent, AddonGuildLeve* addon,
        QuestController.QuestProgress nextQuest)
    {
        _questController.SetPendingQuest(nextQuest);
        _questController.SetNextQuest(null);

        var returnValue = stackalloc AtkValue[1];
        var selectQuest = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 3 },
            new() { Type = ValueType.UInt, UInt = nextQuest.Quest.Id.Value }
        };
        agent->ReceiveEvent(returnValue, selectQuest, 2, 0);
        addon->Close(true);
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "GuildLeve", GuildLevePostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JournalResult", JournalResultPostSetup);
    }
}
