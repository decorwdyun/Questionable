using System;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.V1;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller;

internal sealed class GameUiController : IDisposable
{
    private readonly IClientState _clientState;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IDataManager _dataManager;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;
    private readonly IPluginLog _pluginLog;

    public GameUiController(IClientState clientState, IAddonLifecycle addonLifecycle, IDataManager dataManager,
        GameFunctions gameFunctions, QuestController questController, IPluginLog pluginLog)
    {
        _clientState = clientState;
        _addonLifecycle = addonLifecycle;
        _dataManager = dataManager;
        _gameFunctions = gameFunctions;
        _questController = questController;
        _pluginLog = pluginLog;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
    }

    private unsafe void SelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectString* addonSelectString = (AddonSelectString*)args.Addon;
        string? actualPrompt = addonSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
            return;

        var quest = currentQuest.Quest;
        var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
        if (step == null)
            return;

        foreach (var dialogueChoice in step.DialogueChoices)
        {
            if (dialogueChoice.Answer == null)
                continue;

            string? excelPrompt =
                _gameFunctions.GetExcelString(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
            string? excelAnswer =
                _gameFunctions.GetExcelString(quest, dialogueChoice.ExcelSheet, dialogueChoice.Answer);
            if (excelPrompt == null || actualPrompt != excelPrompt)
                continue;

            for (ushort i = 7; i <= addonSelectString->AtkUnitBase.AtkValuesCount; ++i)
            {
                string? actualAnswer = addonSelectString->AtkUnitBase.AtkValues[i].ReadAtkString();
                if (actualAnswer == null || actualAnswer != excelAnswer)
                    continue;

                _questController.IncreaseDialogueChoicesSelected();
                addonSelectString->AtkUnitBase.FireCallbackInt(i - 7);
                return;
            }
        }
    }

    private unsafe void CutsceneSelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonCutSceneSelectString* addonCutSceneSelectString = (AddonCutSceneSelectString*)args.Addon;
        string? actualPrompt = addonCutSceneSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
            return;

        var quest = currentQuest.Quest;
        var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
        if (step == null)
            return;

        foreach (DialogueChoice dialogueChoice in step.DialogueChoices)
        {
            if (dialogueChoice.Answer == null)
                continue;

            string? excelPrompt =
                _gameFunctions.GetExcelString(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
            string? excelAnswer =
                _gameFunctions.GetExcelString(quest, dialogueChoice.ExcelSheet, dialogueChoice.Answer);
            if (excelPrompt == null || actualPrompt != excelPrompt)
                continue;

            for (int i = 5; i < addonCutSceneSelectString->AtkUnitBase.AtkValuesCount; ++i)
            {
                string? actualAnswer = addonCutSceneSelectString->AtkUnitBase.AtkValues[i].ReadAtkString();
                if (actualAnswer == null || actualAnswer != excelAnswer)
                    continue;

                _questController.IncreaseDialogueChoicesSelected();
                addonCutSceneSelectString->AtkUnitBase.FireCallbackInt(i - 5);
                return;
            }
        }
    }

    private unsafe void SelectYesnoPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectYesno* addonSelectYesno = (AddonSelectYesno*)args.Addon;
        string? actualPrompt = addonSelectYesno->AtkUnitBase.AtkValues[0].ReadAtkString();
        if (actualPrompt == null)
            return;

        _pluginLog.Verbose($"Prompt: '{actualPrompt}'");

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
            return;

        var quest = currentQuest.Quest;
        var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
        if (step != null && HandleDefaultYesNo(addonSelectYesno, quest, step, actualPrompt))
            return;

        HandleTravelYesNo(addonSelectYesno, currentQuest, actualPrompt);
    }

    private unsafe bool HandleDefaultYesNo(AddonSelectYesno* addonSelectYesno, Quest quest, QuestStep step,
        string actualPrompt)
    {
        _pluginLog.Verbose($"DefaultYesNo: Choice count: {step.DialogueChoices.Count}");
        foreach (var dialogueChoice in step.DialogueChoices)
        {
            string? excelPrompt =
                _gameFunctions.GetExcelString(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
            if (excelPrompt == null || actualPrompt != excelPrompt)
                continue;

            addonSelectYesno->AtkUnitBase.FireCallbackInt(dialogueChoice.Yes ? 0 : 1);
            _questController.IncreaseDialogueChoicesSelected();
            return true;
        }

        return false;
    }

    private unsafe bool HandleTravelYesNo(AddonSelectYesno* addonSelectYesno,
        QuestController.QuestProgress currentQuest, string actualPrompt)
    {
        // this can be triggered either manually (in which case we should increase the step counter), or automatically
        // (in which case it is ~1 frame later, and the step counter has already been increased)
        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return false;

        bool increaseStepCount = true;
        QuestStep? step = sequence.FindStep(currentQuest.Step);
        if (step != null)
            _pluginLog.Verbose($"Current step: {step.TerritoryId}, {step.TargetTerritoryId}");

        if (step == null || step.TargetTerritoryId == null || step.TerritoryId != _clientState.TerritoryType)
        {
            _pluginLog.Verbose("TravelYesNo: Checking previous step...");
            step = sequence.FindStep(currentQuest.Step == 255 ? (sequence.Steps.Count - 1) : (currentQuest.Step - 1));
            increaseStepCount = false;

            if (step != null)
                _pluginLog.Verbose($"Previous step: {step.TerritoryId}, {step.TargetTerritoryId}");
        }

        if (step == null || step.TargetTerritoryId == null || step.TerritoryId != _clientState.TerritoryType)
        {
            _pluginLog.Verbose("TravelYesNo: Not found");
            return false;
        }

        var warps = _dataManager.GetExcelSheet<Warp>()!
            .Where(x => x.RowId > 0 && x.TerritoryType.Row == step.TargetTerritoryId)
            .Where(x => x.ConfirmEvent.Row == 0); // unsure if this is needed
        foreach (var entry in warps)
        {
            string? excelPrompt = entry.Question?.ToString();
            if (excelPrompt == null || excelPrompt != actualPrompt)
            {
                _pluginLog.Information($"Ignoring prompt '{excelPrompt}'");
                continue;
            }

            _pluginLog.Information($"Using warp {entry.RowId}, {excelPrompt}");
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            if (increaseStepCount)
                _questController.IncreaseStepCount();
            return true;
        }

        return false;
    }

    private unsafe void CreditPostSetup(AddonEvent type, AddonArgs args)
    {
        _pluginLog.Information("Closing Credits sequence");
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
        addon->FireCallbackInt(-2);
    }

    private unsafe void UnendingCodexPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.CurrentQuest?.Quest.QuestId == 4526)
        {
            _pluginLog.Information("Closing Unending Codex");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
    }
}
