using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model;
using Questionable.Windows;

namespace Questionable;

internal sealed class DalamudInitializer : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;
    private readonly ICommandManager _commandManager;
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly NavigationShortcutController _navigationShortcutController;
    private readonly IChatGui _chatGui;
    private readonly WindowSystem _windowSystem;
    private readonly QuestWindow _questWindow;
    private readonly DebugOverlay _debugOverlay;
    private readonly ConfigWindow _configWindow;
    private readonly QuestRegistry _questRegistry;

    public DalamudInitializer(IDalamudPluginInterface pluginInterface, IFramework framework,
        ICommandManager commandManager, QuestController questController, MovementController movementController,
        GameUiController gameUiController, NavigationShortcutController navigationShortcutController, IChatGui chatGui,
        WindowSystem windowSystem, QuestWindow questWindow, DebugOverlay debugOverlay, ConfigWindow configWindow,
        QuestRegistry questRegistry)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _commandManager = commandManager;
        _questController = questController;
        _movementController = movementController;
        _navigationShortcutController = navigationShortcutController;
        _chatGui = chatGui;
        _windowSystem = windowSystem;
        _questWindow = questWindow;
        _debugOverlay = debugOverlay;
        _configWindow = configWindow;
        _questRegistry = questRegistry;

        _windowSystem.AddWindow(questWindow);
        _windowSystem.AddWindow(configWindow);
        _windowSystem.AddWindow(debugOverlay);

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _questWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _framework.Update += FrameworkUpdate;
        _commandManager.AddHandler("/qst", new CommandInfo(ProcessCommand)
        {
            HelpMessage = "Opens the Questing window"
        });

        _framework.RunOnTick(gameUiController.HandleCurrentDialogueChoices, TimeSpan.FromMilliseconds(200));
    }

    private void FrameworkUpdate(IFramework framework)
    {
        _questController.Update();
        _navigationShortcutController.HandleNavigationShortcut();

        try
        {
            _movementController.Update();
        }
        catch (MovementController.PathfindingFailedException)
        {
            _questController.Stop("Pathfinding failed");
        }
    }

    private void ProcessCommand(string command, string arguments)
    {
        if (arguments is "c" or "config")
            _configWindow.Toggle();
        if (arguments is "start")
            _questController.ExecuteNextStep(true);
        else if (arguments is "stop")
        {
            _movementController.Stop();
            _questController.Stop("Stop command");
        }
        else if (arguments.StartsWith("do", StringComparison.Ordinal))
        {
            if (!_debugOverlay.DrawConditions())
            {
                _chatGui.PrintError("[Questionable] You don't have the debug overlay enabled.");
                return;
            }

            if (arguments.Length >= 4 && ushort.TryParse(arguments.AsSpan(3), out ushort questId))
            {
                if (_questRegistry.IsKnownQuest(questId))
                {
                    _debugOverlay.HighlightedQuest = questId;
                    _chatGui.Print($"[Questionable] Set highlighted quest to {questId}.");
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
        else if (arguments.StartsWith("sim", StringComparison.InvariantCulture))
        {
            string[] parts = arguments.Split(' ');
            if (parts.Length == 2 && ushort.TryParse(parts[1], out ushort questId))
            {
                if (_questRegistry.TryGetQuest(questId, out Quest? quest))
                {
                    _questController.SimulateQuest(quest);
                    _chatGui.Print($"[Questionable] Simulating quest {questId}.");
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
        else if (string.IsNullOrEmpty(arguments))
            _questWindow.Toggle();
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/qst");
        _framework.Update -= FrameworkUpdate;
        _pluginInterface.UiBuilder.OpenMainUi -= _questWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _windowSystem.RemoveAllWindows();
    }
}
