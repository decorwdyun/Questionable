using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Questionable.Controller;
using Questionable.Data;
using Questionable.External;
using Questionable.Windows;

namespace Questionable;

public sealed class QuestionablePlugin : IDalamudPlugin
{
    private readonly WindowSystem _windowSystem = new(nameof(Questionable));

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly IGameGui _gameGui;
    private readonly ICommandManager _commandManager;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;

    private readonly MovementController _movementController;

    public QuestionablePlugin(DalamudPluginInterface pluginInterface, IClientState clientState,
        ITargetManager targetManager, IFramework framework, IGameGui gameGui, IDataManager dataManager,
        ISigScanner sigScanner, IObjectTable objectTable, IPluginLog pluginLog, ICondition condition, IChatGui chatGui,
        ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(sigScanner);
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(objectTable);

        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _framework = framework;
        _gameGui = gameGui;
        _commandManager = commandManager;
        _gameFunctions = new GameFunctions(dataManager, objectTable, sigScanner, targetManager, condition, pluginLog);

        AetheryteData aetheryteData = new AetheryteData(dataManager);
        NavmeshIpc navmeshIpc = new NavmeshIpc(pluginInterface);
        LifestreamIpc lifestreamIpc = new LifestreamIpc(pluginInterface, aetheryteData);
        _movementController =
            new MovementController(navmeshIpc, clientState, _gameFunctions, condition, pluginLog);
        _questController = new QuestController(pluginInterface, dataManager, _clientState, _gameFunctions,
            _movementController, pluginLog, condition, chatGui, aetheryteData, lifestreamIpc);
        _windowSystem.AddWindow(new DebugWindow(_movementController, _questController, _gameFunctions, clientState,
            targetManager));

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _framework.Update += FrameworkUpdate;
        _commandManager.AddHandler("/qst", new CommandInfo(ProcessCommand));

    }

    private void FrameworkUpdate(IFramework framework)
    {
        _questController.Update();

        HandleNavigationShortcut();
        _movementController.Update();
    }

    private void ProcessCommand(string command, string arguments)
    {
        _windowSystem.Windows.Single(x => x is DebugWindow).Toggle();
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
            _movementController.NavigateTo(EMovementType.Shortcut, null, worldPos,
                _gameFunctions.IsFlyingUnlocked(_clientState.TerritoryType));
        }
    }


    public void Dispose()
    {
        _commandManager.RemoveHandler("/qst");
        _framework.Update -= FrameworkUpdate;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;

        _movementController.Dispose();
    }
}
