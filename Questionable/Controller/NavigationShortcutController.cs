using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Questionable.Model;

namespace Questionable.Controller;

internal sealed class NavigationShortcutController
{
    private readonly IGameGui _gameGui;
    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;

    public NavigationShortcutController(IGameGui gameGui, MovementController movementController,
        GameFunctions gameFunctions)
    {
        _gameGui = gameGui;
        _movementController = movementController;
        _gameFunctions = gameFunctions;
    }

    public unsafe void HandleNavigationShortcut()
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
                _gameFunctions.IsFlyingUnlockedInCurrentZone(), true);
        }
    }
}
