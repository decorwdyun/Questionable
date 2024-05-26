using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Questionable.Controller;
using Questionable.External;
using Questionable.Windows;

namespace Questionable;

public sealed class Questionable : IDalamudPlugin
{
    private readonly WindowSystem _windowSystem = new(nameof(Questionable));

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IGameGui _gameGui;
    private readonly GameFunctions _gameFunctions;
    private readonly MovementController _movementController;

    public Questionable(DalamudPluginInterface pluginInterface, IClientState clientState, ITargetManager targetManager,
        IFramework framework, IGameGui gameGui, IDataManager dataManager, ISigScanner sigScanner,
        IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _framework = framework;
        _gameGui = gameGui;
        _gameFunctions = new GameFunctions(dataManager, sigScanner);
        _movementController =
            new MovementController(new NavmeshIpc(pluginInterface), clientState, _gameFunctions, pluginLog);
        _windowSystem.AddWindow(new DebugWindow(_movementController, _gameFunctions, clientState, targetManager));

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _framework.Update += FrameworkUpdate;
    }

    private void FrameworkUpdate(IFramework framework)
    {
        HandleNavigationShortcut();

        _movementController.Update();
    }

    private unsafe void HandleNavigationShortcut()
    {
        var inputData = UIInputData.Instance();
        if (inputData == null)
            return;

        if (inputData->IsGameWindowFocused &&
            inputData->UIFilteredMouseButtonReleasedFlags.HasFlag(MouseButtonFlags.LBUTTON) &&
            inputData->GetKeyState(SeVirtualKey.MENU).HasFlag(KeyStateFlags.Down) &&
            _gameGui.ScreenToWorld(new Vector2(inputData->CursorXPosition, inputData->CursorYPosition),
                out Vector3 worldPos))
        {
            _movementController.NavigateTo(EMovementType.Shortcut, worldPos,
                _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType));
        }
    }


    public void Dispose()
    {
        _framework.Update -= FrameworkUpdate;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _movementController.Dispose();
    }
}
