using System;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller;
using Questionable.Windows;

namespace Questionable;

internal sealed class DalamudInitializer : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly NavigationShortcutController _navigationShortcutController;
    private readonly WindowSystem _windowSystem;
    private readonly QuestWindow _questWindow;
    private readonly ConfigWindow _configWindow;
    private readonly IToastGui _toastGui;
    private readonly ILogger<DalamudInitializer> _logger;

    public DalamudInitializer(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        QuestController questController,
        MovementController movementController,
        GameUiController gameUiController,
        NavigationShortcutController navigationShortcutController,
        WindowSystem windowSystem,
        QuestWindow questWindow,
        DebugOverlay debugOverlay,
        ConfigWindow configWindow,
        QuestSelectionWindow questSelectionWindow,
        QuestValidationWindow questValidationWindow,
        JournalProgressWindow journalProgressWindow,
        IToastGui toastGui,
        ILogger<DalamudInitializer> logger)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _questController = questController;
        _movementController = movementController;
        _navigationShortcutController = navigationShortcutController;
        _windowSystem = windowSystem;
        _questWindow = questWindow;
        _configWindow = configWindow;
        _toastGui = toastGui;
        _logger = logger;

        _windowSystem.AddWindow(questWindow);
        _windowSystem.AddWindow(configWindow);
        _windowSystem.AddWindow(debugOverlay);
        _windowSystem.AddWindow(questSelectionWindow);
        _windowSystem.AddWindow(questValidationWindow);
        _windowSystem.AddWindow(journalProgressWindow);

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _questWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _framework.Update += FrameworkUpdate;
        _framework.RunOnTick(gameUiController.HandleCurrentDialogueChoices, TimeSpan.FromMilliseconds(200));
        _toastGui.Toast += OnToast;
        _toastGui.ErrorToast += OnErrorToast;
        _toastGui.QuestToast += OnQuestToast;
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

    private void OnToast(ref SeString message, ref ToastOptions options, ref bool isHandled)
        => _logger.LogInformation("Normal Toast: {Message}", message);

    private void OnErrorToast(ref SeString message, ref bool isHandled)
        => _logger.LogInformation("Error Toast: {Message}", message);

    private void OnQuestToast(ref SeString message, ref QuestToastOptions options, ref bool isHandled)
        => _logger.LogInformation("Quest Toast: {Message}", message);

    public void Dispose()
    {
        _toastGui.QuestToast -= OnQuestToast;
        _toastGui.ErrorToast -= OnErrorToast;
        _toastGui.Toast -= OnToast;
        _framework.Update -= FrameworkUpdate;
        _pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
        _pluginInterface.UiBuilder.OpenMainUi -= _questWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _windowSystem.RemoveAllWindows();
    }
}
