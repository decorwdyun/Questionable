using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.Windows;
using Questionable.Windows.QuestComponents;

namespace Questionable.Controller;

internal sealed class CommandHandler : IDisposable
{
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly QuestRegistry _questRegistry;
    private readonly ConfigWindow _configWindow;
    private readonly DebugOverlay _debugOverlay;
    private readonly QuestWindow _questWindow;
    private readonly QuestSelectionWindow _questSelectionWindow;
    private readonly ITargetManager _targetManager;
    private readonly QuestFunctions _questFunctions;

    public CommandHandler(
        ICommandManager commandManager,
        IChatGui chatGui,
        QuestController questController,
        MovementController movementController,
        QuestRegistry questRegistry,
        ConfigWindow configWindow,
        DebugOverlay debugOverlay,
        QuestWindow questWindow,
        QuestSelectionWindow questSelectionWindow,
        ITargetManager targetManager,
        QuestFunctions questFunctions)
    {
        _commandManager = commandManager;
        _chatGui = chatGui;
        _questController = questController;
        _movementController = movementController;
        _questRegistry = questRegistry;
        _configWindow = configWindow;
        _debugOverlay = debugOverlay;
        _questWindow = questWindow;
        _questSelectionWindow = questSelectionWindow;
        _targetManager = targetManager;
        _questFunctions = questFunctions;

        _commandManager.AddHandler("/qst", new CommandInfo(ProcessCommand)
        {
            HelpMessage = string.Join($"{Environment.NewLine}\t",
                "Opens the Questing window",
                "/qst config - opens the configuration window",
                "/qst start - starts doing quests",
                "/qst stop - stops doing quests",
                "/qst reload - reload all quest data",
                "/qst which - shows all quests starting with your selected target",
                "/qst zone - shows all quests starting in the current zone (only includes quests with a known quest path, and currently visible unaccepted quests)")
        });
    }

    private void ProcessCommand(string command, string arguments)
    {
        string[] parts = arguments.Split(' ');
        switch (parts[0])
        {
            case "c":
            case "config":
                _configWindow.Toggle();
                break;

            case "start":
                _questWindow.IsOpen = true;
                _questController.ExecuteNextStep(QuestController.EAutomationType.Automatic);
                break;

            case "stop":
                _movementController.Stop();
                _questController.Stop("Stop command");
                break;

            case "reload":
                _questWindow.Reload();
                break;

            case "do":
                ConfigureDebugOverlay(parts.Skip(1).ToArray());
                break;

            case "next":
                SetNextQuest(parts.Skip(1).ToArray());
                break;

            case "sim":
                SetSimulatedQuest(parts.Skip(1).ToArray());
                break;

            case "which":
                _questSelectionWindow.OpenForTarget(_targetManager.Target);
                break;

            case "z":
            case "zone":
                _questSelectionWindow.OpenForCurrentZone();
                break;

            case "":
                _questWindow.Toggle();
                break;

            default:
                _chatGui.PrintError($"Unknown subcommand {parts[0]}", "Questionable");
                break;
        }
    }

    private void ConfigureDebugOverlay(string[] arguments)
    {
        if (!_debugOverlay.DrawConditions())
        {
            _chatGui.PrintError("[Questionable] You don't have the debug overlay enabled.");
            return;
        }

        if (arguments.Length >= 1 && ElementId.TryFromString(arguments[0], out ElementId? questId) && questId != null)
        {
            if (_questRegistry.TryGetQuest(questId, out Quest? quest))
            {
                _debugOverlay.HighlightedQuest = quest.Id;
                _chatGui.Print($"[Questionable] Set highlighted quest to {questId} ({quest.Info.Name}).");
            }
            else
                _chatGui.PrintError($"[Questionable] Unknown quest {questId}.");
        }
        else
        {
            _debugOverlay.HighlightedQuest = null;
            _chatGui.Print("[Questionable] Cleared highlighted quest.");
        }
    }

    private void SetNextQuest(string[] arguments)
    {
        if (arguments.Length >= 1 && ElementId.TryFromString(arguments[0], out ElementId? questId) && questId != null)
        {
            if (_questFunctions.IsQuestLocked(questId))
                _chatGui.PrintError($"[Questionable] Quest {questId} is locked.");
            else if (_questRegistry.TryGetQuest(questId, out Quest? quest))
            {
                _questController.SetNextQuest(quest);
                _chatGui.Print($"[Questionable] Set next quest to {questId} ({quest.Info.Name}).");
            }
            else
            {
                _chatGui.PrintError($"[Questionable] Unknown quest {questId}.");
            }
        }
        else
        {
            _questController.SetNextQuest(null);
            _chatGui.Print("[Questionable] Cleared next quest.");
        }
    }

    private void SetSimulatedQuest(string[] arguments)
    {
        if (arguments.Length >= 1 && ElementId.TryFromString(arguments[0], out ElementId? questId) && questId != null)
        {
            if (_questRegistry.TryGetQuest(questId, out Quest? quest))
            {
                _questController.SimulateQuest(quest);
                _chatGui.Print($"[Questionable] Simulating quest {questId} ({quest.Info.Name}).");
            }
            else
                _chatGui.PrintError($"[Questionable] Unknown quest {questId}.");
        }
        else
        {
            _questController.SimulateQuest(null);
            _chatGui.Print("[Questionable] Cleared simulated quest.");
        }
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/qst");
    }
}
