using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows;

namespace Questionable.Controller;

internal sealed class CommandHandler : IDisposable
{
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly QuestRegistry _questRegistry;
    private readonly QuestData _questData;
    private readonly ConfigWindow _configWindow;
    private readonly DebugOverlay _debugOverlay;
    private readonly QuestWindow _questWindow;

    public CommandHandler(ICommandManager commandManager, IChatGui chatGui, QuestController questController,
        MovementController movementController, QuestRegistry questRegistry, QuestData questData,
        ConfigWindow configWindow, DebugOverlay debugOverlay, QuestWindow questWindow)
    {
        _commandManager = commandManager;
        _chatGui = chatGui;
        _questController = questController;
        _movementController = movementController;
        _questRegistry = questRegistry;
        _questData = questData;
        _configWindow = configWindow;
        _debugOverlay = debugOverlay;
        _questWindow = questWindow;

        _commandManager.AddHandler("/qst", new CommandInfo(ProcessCommand)
        {
            HelpMessage = "Opens the Questing window"
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
                _questController.ExecuteNextStep(true);
                break;

            case "stop":
                _movementController.Stop();
                _questController.Stop("Stop command");
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
                _questData.ShowQuestsIssuedByTarget();
                break;

            default:
                _questWindow.Toggle();
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

        if (arguments.Length >= 1 && ushort.TryParse(arguments[0], out ushort questId))
        {
            if (_questRegistry.TryGetQuest(questId, out Quest? quest))
            {
                _debugOverlay.HighlightedQuest = questId;
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
        if (arguments.Length >= 1 && ushort.TryParse(arguments[0], out ushort questId))
        {
            if (_questRegistry.TryGetQuest(questId, out Quest? quest))
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
        if (arguments.Length >= 1 && ushort.TryParse(arguments[0], out ushort questId))
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
