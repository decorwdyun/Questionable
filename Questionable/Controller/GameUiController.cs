using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib;
using LLib.GameUI;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Interactions;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Quest = Questionable.Model.Quest;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller;

internal sealed class GameUiController : IDisposable
{
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IDataManager _dataManager;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestFunctions _questFunctions;
    private readonly ExcelFunctions _excelFunctions;
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly IGameGui _gameGui;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly ILogger<GameUiController> _logger;
    private readonly Regex _returnRegex;

    public GameUiController(
        IAddonLifecycle addonLifecycle,
        IDataManager dataManager,
        GameFunctions gameFunctions,
        QuestFunctions questFunctions,
        ExcelFunctions excelFunctions,
        QuestController questController,
        QuestRegistry questRegistry,
        QuestData questData,
        IGameGui gameGui,
        ITargetManager targetManager,
        IFramework framework,
        IPluginLog pluginLog,
        ILogger<GameUiController> logger)
    {
        _addonLifecycle = addonLifecycle;
        _dataManager = dataManager;
        _gameFunctions = gameFunctions;
        _questFunctions = questFunctions;
        _excelFunctions = excelFunctions;
        _questController = questController;
        _questRegistry = questRegistry;
        _questData = questData;
        _gameGui = gameGui;
        _targetManager = targetManager;
        _framework = framework;
        _logger = logger;

        _returnRegex = _dataManager.GetExcelSheet<Addon>()!.GetRow(196)!.GetRegex(addon => addon.Text, pluginLog)!;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "PointMenu", PointMenuPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "CreditScroll", CreditScrollPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "HousingSelectBlock", HousingSelectBlockPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", JournalResultPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "GuildLeve", GuildLevePostSetup);
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

    [SuppressMessage("ReSharper", "RedundantJumpStatement")]
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

    private unsafe bool CheckQuestSelection(AddonSelectIconString* addonSelectIconString, Quest quest,
        List<string?> answers)
    {
        // it is possible for this to be a quest selection
        string questName = quest.Info.Name;
        int questSelection = answers.FindIndex(x => GameFunctions.GameStringEquals(questName, x));
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
            answers.Add(addonSelectIconString->AtkValues[i * 3 + 7].ReadAtkString());

        return answers;
    }

    private int? HandleListChoice(string? actualPrompt, List<string?> answers, bool checkAllSteps)
    {
        List<DialogueChoiceInfo> dialogueChoices = [];

        // levequest choices have some vague sort of priority
        if (_questController.HasCurrentTaskMatching<Interact.DoInteract>(out var interact) &&
            interact.Quest != null &&
            interact.InteractionType is EInteractionType.AcceptLeve or EInteractionType.CompleteLeve)
        {
            if (interact.InteractionType == EInteractionType.AcceptLeve)
            {
                dialogueChoices.Add(new DialogueChoiceInfo(interact.Quest,
                    new DialogueChoice
                    {
                        Type = EDialogChoiceType.List,
                        ExcelSheet = "leve/GuildleveAssignment",
                        Prompt = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_TITLE"),
                        Answer = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_01"),
                    }));
                interact.InteractionType = EInteractionType.None;
            }
            else if (interact.InteractionType == EInteractionType.CompleteLeve)
            {
                dialogueChoices.Add(new DialogueChoiceInfo(interact.Quest,
                    new DialogueChoice
                    {
                        Type = EDialogChoiceType.List,
                        ExcelSheet = "leve/GuildleveAssignment",
                        Prompt = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_TITLE"),
                        Answer = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_REWARD"),
                    }));
                interact.InteractionType = EInteractionType.None;
            }
        }

        var currentQuest = _questController.SimulatedQuest ??
                           _questController.GatheringQuest ??
                           _questController.StartedQuest;
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

            // add all travel dialogue choices
            var targetTerritoryId = FindTargetTerritoryFromQuestStep(currentQuest);
            if (targetTerritoryId != null)
            {
                foreach (string? answer in answers)
                {
                    if (answer == null)
                        continue;

                    if (TryFindWarp(targetTerritoryId.Value, answer, out uint? warpId, out string? warpText))
                    {
                        _logger.LogInformation("Adding warp {Id}, {Prompt}", warpId, warpText);
                        dialogueChoices.Add(new DialogueChoiceInfo(quest, new DialogueChoice
                        {
                            Type = EDialogChoiceType.List,
                            ExcelSheet = null,
                            Prompt = null,
                            Answer = ExcelRef.FromSheetValue(warpText),
                        }));
                    }
                }
            }
        }
        else
            _logger.LogDebug("Ignoring current quest dialogue choices, no active quest");

        // add all quests that start with the targeted npc
        var target = _targetManager.Target;
        if (target != null)
        {
            foreach (var questInfo in _questData.GetAllByIssuerDataId(target.DataId).Where(x => x.QuestId is QuestId))
            {
                if (_questFunctions.IsReadyToAcceptQuest(questInfo.QuestId) &&
                    _questRegistry.TryGetQuest(questInfo.QuestId, out Quest? knownQuest))
                {
                    var questChoices = knownQuest.FindSequence(0)?.Steps
                        .SelectMany(x => x.DialogueChoices)
                        .ToList();
                    if (questChoices != null && questChoices.Count > 0)
                    {
                        _logger.LogInformation("Adding {Count} dialogue choices from not accepted quest {QuestName}",
                            questChoices.Count, questInfo.Name);
                        dialogueChoices.AddRange(questChoices.Select(x => new DialogueChoiceInfo(knownQuest, x)));
                    }
                }
            }

            if ((_questController.IsRunning || _questController.WasLastTaskUpdateWithin(TimeSpan.FromSeconds(5)))
                && _questController.NextQuest == null)
            {
                // make sure to always close the leve dialogue
                if (_questData.GetAllByIssuerDataId(target.DataId).Any(x => x.QuestId is LeveId))
                {
                    _logger.LogInformation("Adding close leve dialogue as option");
                    dialogueChoices.Add(new DialogueChoiceInfo(null,
                        new DialogueChoice
                        {
                            Type = EDialogChoiceType.List,
                            ExcelSheet = "leve/GuildleveAssignment",
                            Prompt = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_TITLE"),
                            Answer = new ExcelRef("TEXT_GUILDLEVEASSIGNMENT_SELECT_MENU_07"),
                        }));
                }
            }
        }

        if (dialogueChoices.Count == 0)
        {
            _logger.LogDebug("No dialogue choices to check");
            return null;
        }

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

            string? excelPrompt = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt, false)
                ?.GetString();
            StringOrRegex? excelAnswer = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Answer,
                dialogueChoice.AnswerIsRegularExpression);

            if (actualPrompt == null && !string.IsNullOrEmpty(excelPrompt))
            {
                _logger.LogInformation("Unexpected excelPrompt: {ExcelPrompt}", excelPrompt);
                continue;
            }

            if (actualPrompt != null &&
                (excelPrompt == null || !GameFunctions.GameStringEquals(actualPrompt, excelPrompt)))
            {
                _logger.LogInformation("Unexpected excelPrompt: {ExcelPrompt}, actualPrompt: {ActualPrompt}",
                    excelPrompt, actualPrompt);
                continue;
            }

            for (int i = 0; i < answers.Count; ++i)
            {
                _logger.LogTrace("Checking if {ActualAnswer} == {ExpectedAnswer}",
                    answers[i], excelAnswer);
                if (IsMatch(answers[i], excelAnswer))
                {
                    _logger.LogInformation("Returning {Index}: '{Answer}' for '{Prompt}'",
                        i, answers[i], actualPrompt);

                    // ensure we only open the dialog once
                    if (quest?.Id is SatisfactionSupplyNpcId)
                    {
                        if (_questController.GatheringQuest == null ||
                            _questController.GatheringQuest.Sequence == 255)
                            return null;

                        _questController.GatheringQuest.SetSequence(1);
                        _questController.ExecuteNextStep(QuestController.EAutomationType.CurrentQuestOnly);
                    }

                    return i;
                }
            }
        }

        _logger.LogInformation("No matching answer found for {Prompt}.", actualPrompt);
        return null;
    }

    private static bool IsMatch(string? actualAnswer, StringOrRegex? expectedAnswer)
    {
        if (actualAnswer == null && expectedAnswer == null)
            return true;

        if (actualAnswer == null || expectedAnswer == null)
            return false;

        return expectedAnswer.IsMatch(actualAnswer);
    }

    private int? HandleInstanceListChoice(string? actualPrompt)
    {
        if (!_questController.IsRunning)
            return null;

        string? expectedPrompt = _excelFunctions.GetDialogueTextByRowId("Addon", 2090, false).GetString();
        if (GameFunctions.GameStringEquals(actualPrompt, expectedPrompt))
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

    [SuppressMessage("ReSharper", "RedundantJumpStatement")]
    private unsafe void SelectYesnoPostSetup(AddonSelectYesno* addonSelectYesno, bool checkAllSteps)
    {
        string? actualPrompt = addonSelectYesno->AtkUnitBase.AtkValues[0].ReadAtkString();
        if (actualPrompt == null)
            return;

        _logger.LogTrace("Prompt: '{Prompt}'", actualPrompt);
        var director = UIState.Instance()->DirectorTodo.Director;
        if (director != null && director->EventHandlerInfo != null &&
            director->EventHandlerInfo->EventId.ContentId == EventHandlerType.GatheringLeveDirector &&
            director->Sequence == 254)
        {
            // just close the dialogue for 'do you want to return to next settlement', should prolly be different for
            // ARR territories
            addonSelectYesno->AtkUnitBase.FireCallbackInt(1);
            return;
        }

        var currentQuest = _questController.StartedQuest;
        if (currentQuest != null && CheckQuestYesNo(addonSelectYesno, currentQuest, actualPrompt, checkAllSteps))
            return;

        var simulatedQuest = _questController.SimulatedQuest;
        if (simulatedQuest != null && HandleTravelYesNo(addonSelectYesno, simulatedQuest, actualPrompt))
            return;

        var nextQuest = _questController.NextQuest;
        if (nextQuest != null && CheckQuestYesNo(addonSelectYesno, nextQuest, actualPrompt, checkAllSteps))
            return;

        return;
    }

    private unsafe bool CheckQuestYesNo(AddonSelectYesno* addonSelectYesno, QuestController.QuestProgress currentQuest,
        string actualPrompt, bool checkAllSteps)
    {
        var quest = currentQuest.Quest;
        if (checkAllSteps)
        {
            var sequence = quest.FindSequence(currentQuest.Sequence);
            if (sequence != null && HandleDefaultYesNo(addonSelectYesno, quest,
                    sequence.Steps.SelectMany(x => x.DialogueChoices).ToList(), actualPrompt))
                return true;
        }
        else
        {
            var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
            if (step != null && HandleDefaultYesNo(addonSelectYesno, quest, step.DialogueChoices, actualPrompt))
                return true;
        }

        if (currentQuest.Quest.Id is LeveId)
        {
            var dialogueChoice = new DialogueChoice
            {
                Type = EDialogChoiceType.YesNo,
                ExcelSheet = "Addon",
                Prompt = new ExcelRef(608),
                Yes = true
            };

            if (HandleDefaultYesNo(addonSelectYesno, quest, [dialogueChoice], actualPrompt))
                return true;
        }

        if (HandleTravelYesNo(addonSelectYesno, currentQuest, actualPrompt))
            return true;

        return false;
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

            string? excelPrompt = ResolveReference(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt, false)
                ?.GetString();
            if (excelPrompt == null || !GameFunctions.GameStringEquals(actualPrompt, excelPrompt))
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

    private unsafe bool HandleTravelYesNo(AddonSelectYesno* addonSelectYesno,
        QuestController.QuestProgress currentQuest, string actualPrompt)
    {
        if (_gameFunctions.ReturnRequestedAt >= DateTime.Now.AddSeconds(-2) && _returnRegex.IsMatch(actualPrompt))
        {
            _logger.LogInformation("Automatically confirming return...");
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            return true;
        }

        if (_questController.IsRunning && _gameGui.TryGetAddonByName("HousingSelectBlock", out AtkUnitBase* _))
        {
            _logger.LogInformation("Automatically confirming ward selection");
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            return true;
        }

        var targetTerritoryId = FindTargetTerritoryFromQuestStep(currentQuest);
        if (targetTerritoryId != null &&
            TryFindWarp(targetTerritoryId.Value, actualPrompt, out uint? warpId, out string? warpText))
        {
            _logger.LogInformation("Using warp {Id}, {Prompt}", warpId, warpText);
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            return true;
        }

        return false;
    }

    private ushort? FindTargetTerritoryFromQuestStep(QuestController.QuestProgress currentQuest)
    {
        // this can be triggered either manually (in which case we should increase the step counter), or automatically
        // (in which case it is ~1 frame later, and the step counter has already been increased)
        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return null;

        QuestStep? step = sequence.FindStep(currentQuest.Step);
        if (step != null)
            _logger.LogTrace("FindTargetTerritoryFromQuestStep (current): {CurrentTerritory}, {TargetTerritory}",
                step.TerritoryId,
                step.TargetTerritoryId);

        if (step == null || step.TargetTerritoryId == null)
        {
            _logger.LogTrace("FindTargetTerritoryFromQuestStep: Checking previous step...");
            step = sequence.FindStep(currentQuest.Step == 255 ? (sequence.Steps.Count - 1) : (currentQuest.Step - 1));

            if (step != null)
                _logger.LogTrace("FindTargetTerritoryFromQuestStep (previous): {CurrentTerritory}, {TargetTerritory}",
                    step.TerritoryId,
                    step.TargetTerritoryId);
        }

        if (step == null || step.TargetTerritoryId == null)
        {
            _logger.LogTrace("FindTargetTerritoryFromQuestStep: Not found");
            return null;
        }

        _logger.LogDebug("Target territory for quest step: {TargetTerritory}", step.TargetTerritoryId);
        return step.TargetTerritoryId;
    }

    private bool TryFindWarp(ushort targetTerritoryId, string actualPrompt, [NotNullWhen(true)] out uint? warpId,
        [NotNullWhen(true)] out string? warpText)
    {
        var warps = _dataManager.GetExcelSheet<Warp>()!
            .Where(x => x.RowId > 0 && x.TerritoryType.Row == targetTerritoryId);
        foreach (var entry in warps)
        {
            string? excelName = entry.Name?.ToString();
            string? excelQuestion = entry.Question?.ToString();

            if (excelQuestion != null && GameFunctions.GameStringEquals(excelQuestion, actualPrompt))
            {
                warpId = entry.RowId;
                warpText = excelQuestion;
                return true;
            }
            else if (excelName != null && GameFunctions.GameStringEquals(excelName, actualPrompt))
            {
                warpId = entry.RowId;
                warpText = excelName;
                return true;
            }
            else
            {
                _logger.LogDebug("Ignoring prompt '{Prompt}'", excelQuestion);
            }
        }

        warpId = null;
        warpText = null;
        return false;
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

    /// <summary>
    /// ARR Credits.
    /// </summary>
    private unsafe void CreditScrollPostSetup(AddonEvent type, AddonArgs args)
    {
        _logger.LogInformation("Closing Credits sequence");
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
        addon->FireCallbackInt(-2);
    }

    /// <summary>
    /// Credits for (possibly all?) expansions, not used for ARR.
    /// </summary>
    private unsafe void CreditPostSetup(AddonEvent type, AddonArgs args)
    {
        _logger.LogInformation("Closing Credits sequence");
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
        addon->FireCallbackInt(-2);
    }

    private unsafe void UnendingCodexPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 4526)
        {
            _logger.LogInformation("Closing Unending Codex");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
        }
    }

    private unsafe void ContentsTutorialPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 245)
        {
            _logger.LogInformation("Closing ContentsTutorial");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(13);
        }
    }

    /// <summary>
    /// Opened e.g. the first time you open the duty finder window during Sastasha.
    /// </summary>
    private unsafe void MultipleHelpWindowPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.StartedQuest?.Quest.Id.Value == 245)
        {
            _logger.LogInformation("Closing MultipleHelpWindow");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
            addon->FireCallbackInt(-1);
        }
    }

    private unsafe void HousingSelectBlockPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.IsRunning)
        {
            _logger.LogInformation("Confirming selected housing ward");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(0);
        }
    }

    private unsafe void JournalResultPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.IsRunning)
        {
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
    }

    private unsafe void GuildLevePostSetup(AddonEvent type, AddonArgs args)
    {
        var target = _targetManager.Target;
        if (target == null)
            return;

        if (_questController is { IsRunning: true, NextQuest: { Quest.Id: LeveId } nextQuest } &&
            _questFunctions.IsReadyToAcceptQuest(nextQuest.Quest.Id))
        {
            var addon = (AddonGuildLeve*)args.Addon;
            /*
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

            _framework.RunOnTick(() =>
            {
                _questController.SetPendingQuest(nextQuest);
                _questController.SetNextQuest(null);

                var agent = UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.LeveQuest);
                var returnValue = stackalloc AtkValue[1];
                var selectQuest = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 3 },
                    new() { Type = ValueType.UInt, UInt = nextQuest.Quest.Id.Value }
                };
                agent->ReceiveEvent(returnValue, selectQuest, 2, 0);
                addon->Close(true);
            }, TimeSpan.FromMilliseconds(100));
        }
    }

    private StringOrRegex? ResolveReference(Quest? quest, string? excelSheet, ExcelRef? excelRef, bool isRegExp)
    {
        if (excelRef == null)
            return null;

        if (excelRef.Type == ExcelRef.EType.Key)
            return _excelFunctions.GetDialogueText(quest, excelSheet, excelRef.AsKey(), isRegExp);
        else if (excelRef.Type == ExcelRef.EType.RowId)
            return _excelFunctions.GetDialogueTextByRowId(excelSheet, excelRef.AsRowId(), isRegExp);
        else if (excelRef.Type == ExcelRef.EType.RawString)
            return new StringOrRegex(excelRef.AsRawString());

        return null;
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "GuildLeve", GuildLevePostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JournalResult", JournalResultPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "HousingSelectBlock", HousingSelectBlockPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "MultipleHelpWindow", MultipleHelpWindowPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ContentsTutorial", ContentsTutorialPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CreditScroll", CreditScrollPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "PointMenu", PointMenuPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
    }

    private sealed record DialogueChoiceInfo(Quest? Quest, DialogueChoice DialogueChoice);
}
