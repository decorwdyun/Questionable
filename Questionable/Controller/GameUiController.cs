using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib;
using LLib.GameUI;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Model.V1;
using Quest = Questionable.Model.Quest;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller;

internal sealed class GameUiController : IDisposable
{
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IDataManager _dataManager;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly IGameGui _gameGui;
    private readonly ITargetManager _targetManager;
    private readonly ILogger<GameUiController> _logger;
    private readonly Regex _returnRegex;

    public GameUiController(IAddonLifecycle addonLifecycle, IDataManager dataManager, GameFunctions gameFunctions,
        QuestController questController, QuestRegistry questRegistry, QuestData questData, IGameGui gameGui,
        ITargetManager targetManager, IPluginLog pluginLog, ILogger<GameUiController> logger)
    {
        _addonLifecycle = addonLifecycle;
        _dataManager = dataManager;
        _gameFunctions = gameFunctions;
        _questController = questController;
        _questRegistry = questRegistry;
        _questData = questData;
        _gameGui = gameGui;
        _targetManager = targetManager;
        _logger = logger;

        _returnRegex = _dataManager.GetExcelSheet<Addon>()!.GetRow(196)!.GetRegex(addon => addon.Text, pluginLog)!;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "PointMenu", PointMenuPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
    }

    internal unsafe void HandleCurrentDialogueChoices()
    {
        if (_gameGui.TryGetAddonByName("SelectString", out AddonSelectString* addonSelectString))
        {
            _logger.LogInformation("SelectString window is open");
            SelectStringPostSetup(addonSelectString, true);
        }

        if (_gameGui.TryGetAddonByName("CutSceneSelectString",
                out AddonCutSceneSelectString* addonCutSceneSelectString))
        {
            _logger.LogInformation("CutSceneSelectString window is open");
            CutsceneSelectStringPostSetup(addonCutSceneSelectString, true);
        }

        if (_gameGui.TryGetAddonByName("SelectIconString", out AddonSelectIconString* addonSelectIconString))
        {
            _logger.LogInformation("SelectIconString window is open");
            SelectIconStringPostSetup(addonSelectIconString, true);
        }

        if (_gameGui.TryGetAddonByName("SelectYesno", out AddonSelectYesno* addonSelectYesno))
        {
            _logger.LogInformation("SelectYesno window is open");
            SelectYesnoPostSetup(addonSelectYesno, true);
        }

        if (_gameGui.TryGetAddonByName("PointMenu", out AtkUnitBase* addonPointMenu))
        {
            _logger.LogInformation("PointMenu is open");
            PointMenuPostSetup(addonPointMenu);
        }
    }

    private unsafe void SelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectString* addonSelectString = (AddonSelectString*)args.Addon;
        SelectStringPostSetup(addonSelectString, false);
    }

    private unsafe void SelectStringPostSetup(AddonSelectString* addonSelectString, bool checkAllSteps)
    {
        string? actualPrompt = addonSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        List<string?> answers = new();
        for (ushort i = 7; i < addonSelectString->AtkUnitBase.AtkValuesCount; ++i)
        {
            if (addonSelectString->AtkUnitBase.AtkValues[i].Type == ValueType.String)
                answers.Add(addonSelectString->AtkUnitBase.AtkValues[i].ReadAtkString());
        }

        int? answer = HandleListChoice(actualPrompt, answers, checkAllSteps) ?? HandleInstanceListChoice(actualPrompt);
        if (answer != null)
            addonSelectString->AtkUnitBase.FireCallbackInt(answer.Value);
    }

    private unsafe void CutsceneSelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonCutSceneSelectString* addonCutSceneSelectString = (AddonCutSceneSelectString*)args.Addon;
        CutsceneSelectStringPostSetup(addonCutSceneSelectString, false);
    }

    private unsafe void CutsceneSelectStringPostSetup(AddonCutSceneSelectString* addonCutSceneSelectString,
        bool checkAllSteps)
    {
        string? actualPrompt = addonCutSceneSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        List<string?> answers = new();
        for (int i = 5; i < addonCutSceneSelectString->AtkUnitBase.AtkValuesCount; ++i)
            answers.Add(addonCutSceneSelectString->AtkUnitBase.AtkValues[i].ReadAtkString());

        int? answer = HandleListChoice(actualPrompt, answers, checkAllSteps);
        if (answer != null)
            addonCutSceneSelectString->AtkUnitBase.FireCallbackInt(answer.Value);
    }

    private unsafe void SelectIconStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectIconString* addonSelectIconString = (AddonSelectIconString*)args.Addon;
        SelectIconStringPostSetup(addonSelectIconString, false);
    }

    private unsafe void SelectIconStringPostSetup(AddonSelectIconString* addonSelectIconString, bool checkAllSteps)
    {
        string? actualPrompt = addonSelectIconString->AtkUnitBase.AtkValues[3].ReadAtkString();
        if (string.IsNullOrEmpty(actualPrompt))
            actualPrompt = null;

        var answers = GetChoices(addonSelectIconString);
        int? answer = HandleListChoice(actualPrompt, answers, checkAllSteps);
        if (answer != null)
        {
            addonSelectIconString->AtkUnitBase.FireCallbackInt(answer.Value);
            return;
        }

        // this is 'Daily Quests' for tribal quests, but not set for normal selections
        string? title = addonSelectIconString->AtkValues[0].ReadAtkString();

        var currentQuest = _questController.StartedQuest;
        if (currentQuest != null && (actualPrompt == null || title != null))
        {
            _logger.LogInformation("Checking if current quest {Name} is on the list", currentQuest.Quest.Info.Name);
            if (CheckQuestSelection(addonSelectIconString, currentQuest.Quest, answers))
                return;
        }

        var nextQuest = _questController.NextQuest;
        if (nextQuest != null && (actualPrompt == null || title != null))
        {
            _logger.LogInformation("Checking if next quest {Name} is on the list", nextQuest.Quest.Info.Name);
            if (CheckQuestSelection(addonSelectIconString, nextQuest.Quest, answers))
                return;
        }
    }

    private unsafe bool CheckQuestSelection(AddonSelectIconString* addonSelectIconString, Quest quest, List<string?> answers)
    {
        // it is possible for this to be a quest selection
        string questName = quest.Info.Name;
        int questSelection = answers.FindIndex(x => GameStringEquals(questName, x));
        if (questSelection >= 0)
        {
            addonSelectIconString->AtkUnitBase.FireCallbackInt(questSelection);
            return true;
        }

        return false;
    }

    public static unsafe List<string?> GetChoices(AddonSelectIconString* addonSelectIconString)
    {
        List<string?> answers = new();
        for (ushort i = 0; i < addonSelectIconString->AtkUnitBase.AtkValues[5].Int; i++)
            answers.Add( addonSelectIconString->AtkValues[i * 3 + 7].ReadAtkString());

        return answers;
    }

    private int? HandleListChoice(string? actualPrompt, List<string?> answers, bool checkAllSteps)
    {
        List<DialogueChoiceInfo> dialogueChoices = [];
        var currentQuest = _questController.StartedQuest;
        if (currentQuest != null)
        {
            var quest = currentQuest.Quest;
            if (checkAllSteps)
            {
                var sequence = quest.FindSequence(currentQuest.Sequence);
                var choices = sequence?.Steps.SelectMany(x => x.DialogueChoices);
                if (choices != null)
                    dialogueChoices.AddRange(choices.Select(x => new DialogueChoiceInfo(quest, x)));
            }
            else
            {
                var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
                if (step == null)
                    _logger.LogDebug("Ignoring current quest dialogue choices, no active step");
                else
                    dialogueChoices.AddRange(step.DialogueChoices.Select(x => new DialogueChoiceInfo(quest, x)));
            }
        }
        else
            _logger.LogDebug("Ignoring current quest dialogue choices, no active quest");

        // add all quests that start with the targeted npc
        var target = _targetManager.Target;
        if (target != null)
        {
            foreach (var questInfo in _questData.GetAllByIssuerDataId(target.DataId))
            {
                if (_gameFunctions.IsReadyToAcceptQuest(questInfo.QuestId) &&
                    _questRegistry.TryGetQuest(questInfo.QuestId, out Quest? knownQuest))
                {
                    var questChoices = knownQuest.FindSequence(0)?.Steps
                        .SelectMany(x => x.DialogueChoices)
                        .ToList();
                    if (questChoices != null && questChoices.Count > 0)
                    {
                        _logger.LogInformation("Adding {Count} dialogue choices from not accepted quest {QuestName}", questChoices.Count, questInfo.Name);
                        dialogueChoices.AddRange(questChoices.Select(x => new DialogueChoiceInfo(knownQuest, x)));
                    }
                }
            }
        }

        if (dialogueChoices.Count == 0)
            return null;

        foreach (var (quest, dialogueChoice) in dialogueChoices)
        {
            if (dialogueChoice.Type != EDialogChoiceType.List)
                continue;

            if (dialogueChoice.Answer == null)
            {
                _logger.LogDebug("Ignoring entry in DialogueChoices, no answer");
                continue;
            }

            if (dialogueChoice.DataId != null && dialogueChoice.DataId != _targetManager.Target?.DataId)
            {
                _logger.LogDebug(
                    "Skipping entry in DialogueChoice expecting target dataId {ExpectedDataId}, actual target is {ActualTargetId}",
                    dialogueChoice.DataId, _targetManager.Target?.DataId);
                continue;
            }

            string? excelPrompt = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
            string? excelAnswer = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Answer);

            if (actualPrompt == null && !string.IsNullOrEmpty(excelPrompt))
            {
                _logger.LogInformation("Unexpected excelPrompt: {ExcelPrompt}", excelPrompt);
                continue;
            }

            if (actualPrompt != null && (excelPrompt == null || !GameStringEquals(actualPrompt, excelPrompt)))
            {
                _logger.LogInformation("Unexpected excelPrompt: {ExcelPrompt}, actualPrompt: {ActualPrompt}",
                    excelPrompt, actualPrompt);
                continue;
            }

            for (int i = 0; i < answers.Count; ++i)
            {
                _logger.LogTrace("Checking if {ActualAnswer} == {ExpectedAnswer}",
                    answers[i], excelAnswer);
                if (GameStringEquals(answers[i], excelAnswer))
                {
                    _logger.LogInformation("Returning {Index}: '{Answer}' for '{Prompt}'",
                        i, answers[i], actualPrompt);
                    return i;
                }
            }
        }

        _logger.LogInformation("No matching answer found for {Prompt}.", actualPrompt);
        return null;
    }

    private int? HandleInstanceListChoice(string? actualPrompt)
    {
        if (!_questController.IsRunning)
            return null;

        string? expectedPrompt = _gameFunctions.GetDialogueTextByRowId("Addon", 2090);
        if (GameStringEquals(actualPrompt, expectedPrompt))
        {
            _logger.LogInformation("Selecting no prefered instance as answer for '{Prompt}'", actualPrompt);
            return 0; // any instance
        }

        return null;
    }

    private unsafe void SelectYesnoPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectYesno* addonSelectYesno = (AddonSelectYesno*)args.Addon;
        SelectYesnoPostSetup(addonSelectYesno, false);
    }

    private unsafe void SelectYesnoPostSetup(AddonSelectYesno* addonSelectYesno, bool checkAllSteps)
    {
        string? actualPrompt = addonSelectYesno->AtkUnitBase.AtkValues[0].ReadAtkString();
        if (actualPrompt == null)
            return;

        _logger.LogTrace("Prompt: '{Prompt}'", actualPrompt);

        var currentQuest = _questController.StartedQuest;
        if (currentQuest == null)
            return;

        var quest = currentQuest.Quest;
        if (checkAllSteps)
        {
            var sequence = quest.FindSequence(currentQuest.Sequence);
            if (sequence != null && HandleDefaultYesNo(addonSelectYesno, quest,
                    sequence.Steps.SelectMany(x => x.DialogueChoices).ToList(), actualPrompt))
                return;
        }
        else
        {
            var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
            if (step != null && HandleDefaultYesNo(addonSelectYesno, quest, step.DialogueChoices, actualPrompt))
                return;
        }

        HandleTravelYesNo(addonSelectYesno, currentQuest, actualPrompt);
    }

    private unsafe bool HandleDefaultYesNo(AddonSelectYesno* addonSelectYesno, Quest quest,
        IList<DialogueChoice> dialogueChoices, string actualPrompt)
    {
        _logger.LogTrace("DefaultYesNo: Choice count: {Count}", dialogueChoices.Count);
        foreach (var dialogueChoice in dialogueChoices)
        {
            if (dialogueChoice.Type != EDialogChoiceType.YesNo)
                continue;

            if (dialogueChoice.DataId != null && dialogueChoice.DataId != _targetManager.Target?.DataId)
            {
                _logger.LogDebug(
                    "Skipping entry in DialogueChoice expecting target dataId {ExpectedDataId}, actual target is {ActualTargetId}",
                    dialogueChoice.DataId, _targetManager.Target?.DataId);
                continue;
            }

            string? excelPrompt = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
            if (excelPrompt == null || !GameStringEquals(actualPrompt, excelPrompt))
            {
                _logger.LogInformation("Unexpected excelPrompt: {ExcelPrompt}, actualPrompt: {ActualPrompt}",
                    excelPrompt, actualPrompt);
                continue;
            }

            addonSelectYesno->AtkUnitBase.FireCallbackInt(dialogueChoice.Yes ? 0 : 1);
            return true;
        }

        return false;
    }

    private unsafe void HandleTravelYesNo(AddonSelectYesno* addonSelectYesno,
        QuestController.QuestProgress currentQuest, string actualPrompt)
    {
        if (_gameFunctions.ReturnRequestedAt >= DateTime.Now.AddSeconds(-2) && _returnRegex.IsMatch(actualPrompt))
        {
            _logger.LogInformation("Automatically confirming return...");
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            return;
        }

        // this can be triggered either manually (in which case we should increase the step counter), or automatically
        // (in which case it is ~1 frame later, and the step counter has already been increased)
        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return;

        QuestStep? step = sequence.FindStep(currentQuest.Step);
        if (step != null)
            _logger.LogTrace("Current step: {CurrentTerritory}, {TargetTerritory}", step.TerritoryId,
                step.TargetTerritoryId);

        if (step == null || step.TargetTerritoryId == null)
        {
            _logger.LogTrace("TravelYesNo: Checking previous step...");
            step = sequence.FindStep(currentQuest.Step == 255 ? (sequence.Steps.Count - 1) : (currentQuest.Step - 1));

            if (step != null)
                _logger.LogTrace("Previous step: {CurrentTerritory}, {TargetTerritory}", step.TerritoryId,
                    step.TargetTerritoryId);
        }

        if (step == null || step.TargetTerritoryId == null)
        {
            _logger.LogTrace("TravelYesNo: Not found");
            return;
        }

        var warps = _dataManager.GetExcelSheet<Warp>()!
            .Where(x => x.RowId > 0 && x.TerritoryType.Row == step.TargetTerritoryId);
        foreach (var entry in warps)
        {
            string? excelPrompt = entry.Question?.ToString();
            if (excelPrompt == null || !GameStringEquals(excelPrompt, actualPrompt))
            {
                _logger.LogDebug("Ignoring prompt '{Prompt}'", excelPrompt);
                continue;
            }

            _logger.LogInformation("Using warp {Id}, {Prompt}", entry.RowId, excelPrompt);
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            return;
        }
    }

    private unsafe void PointMenuPostSetup(AddonEvent type, AddonArgs args)
    {
        AtkUnitBase* addonPointMenu = (AtkUnitBase*)args.Addon;
        PointMenuPostSetup(addonPointMenu);
    }

    private unsafe void PointMenuPostSetup(AtkUnitBase* addonPointMenu)
    {
        var currentQuest = _questController.StartedQuest;
        if (currentQuest == null)
        {
            _logger.LogInformation("Ignoring point menu, no active quest");
            return;
        }

        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return;

        QuestStep? step = sequence.FindStep(currentQuest.Step);
        if (step == null)
            return;

        if (step.PointMenuChoices.Count == 0)
        {
            _logger.LogWarning("No point menu choices");
            return;
        }

        int counter = currentQuest.StepProgress.PointMenuCounter;
        if (counter >= step.PointMenuChoices.Count)
        {
            _logger.LogWarning("No remaining point menu choices");
            return;
        }

        uint choice = step.PointMenuChoices[counter];

        _logger.LogInformation("Handling point menu, picking choice {Choice} (index = {Index})", choice, counter);
        var selectChoice = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 13 },
            new() { Type = ValueType.UInt, UInt = choice }
        };
        addonPointMenu->FireCallback(2, selectChoice);

        currentQuest.IncreasePointMenuCounter();
    }

    private unsafe void CreditPostSetup(AddonEvent type, AddonArgs args)
    {
        _logger.LogInformation("Closing Credits sequence");
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
        addon->FireCallbackInt(-2);
    }

    private unsafe void UnendingCodexPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.QuestId == 4526)
        {
            _logger.LogInformation("Closing Unending Codex");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
        }
    }

    private unsafe void ContentsTutorialPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.QuestId == 245)
        {
            _logger.LogInformation("Closing ContentsTutorial");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(13);
        }
    }

    private unsafe void MultipleHelpWindowPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.QuestId == 245)
        {
            _logger.LogInformation("Closing MultipleHelpWindow");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
            addon->FireCallbackInt(-1);
        }
    }

    /// <summary>
    /// Ensures characters like '-' are handled equally in both strings.
    /// </summary>
    public static bool GameStringEquals(string? a, string? b)
    {
        if (a == null)
            return b == null;

        if (b == null)
            return false;

        return a.ReplaceLineEndings().Replace('\u2013', '-') == b.ReplaceLineEndings().Replace('\u2013', '-');
    }

    private string? ResolveReference(Quest quest, string? excelSheet, ExcelRef? excelRef)
    {
        if (excelRef == null)
            return null;

        if (excelRef.Type == ExcelRef.EType.Key)
            return _gameFunctions.GetDialogueText(quest, excelSheet, excelRef.AsKey());
        else if (excelRef.Type == ExcelRef.EType.RowId)
            return _gameFunctions.GetDialogueTextByRowId(excelSheet, excelRef.AsRowId());

        return null;
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "PointMenu", PointMenuPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
    }

    private sealed record DialogueChoiceInfo(Quest Quest, DialogueChoice DialogueChoice);
}
