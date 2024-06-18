using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Questionable.Controller;
using Questionable.Windows;

namespace Questionable;

internal sealed class DalamudInitializer : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;
    private readonly ICommandManager _commandManager;
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly NavigationShortcutController _navigationShortcutController;
    private readonly WindowSystem _windowSystem;
    private readonly QuestWindow _questWindow;
    private readonly ConfigWindow _configWindow;

    public DalamudInitializer(DalamudPluginInterface pluginInterface, IFramework framework,
        ICommandManager commandManager, QuestController questController, MovementController movementController,
        GameUiController gameUiController, NavigationShortcutController navigationShortcutController,
        WindowSystem windowSystem, QuestWindow questWindow, DebugOverlay debugOverlay, ConfigWindow configWindow)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _commandManager = commandManager;
        _questController = questController;
        _movementController = movementController;
        _navigationShortcutController = navigationShortcutController;
        _windowSystem = windowSystem;
        _questWindow = questWindow;
        _configWindow = configWindow;

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
        else
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
