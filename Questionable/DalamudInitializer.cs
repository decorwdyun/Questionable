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
    private readonly DebugWindow _debugWindow;

    public DalamudInitializer(DalamudPluginInterface pluginInterface, IFramework framework,
        ICommandManager commandManager, QuestController questController, MovementController movementController,
        GameUiController gameUiController, NavigationShortcutController navigationShortcutController, WindowSystem windowSystem, DebugWindow debugWindow)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _commandManager = commandManager;
        _questController = questController;
        _movementController = movementController;
        _navigationShortcutController = navigationShortcutController;
        _windowSystem = windowSystem;
        _debugWindow = debugWindow;

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _debugWindow.Toggle;
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
            _questController.Stop();
        }
    }

    private void ProcessCommand(string command, string arguments)
    {
        _debugWindow.Toggle();
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/qst");
        _framework.Update -= FrameworkUpdate;
        _pluginInterface.UiBuilder.OpenMainUi -= _debugWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
    }
}
