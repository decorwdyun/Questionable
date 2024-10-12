using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.CombatModules;

/// <summary>
/// Commandeered Magitek Armor; used in 'Magiteknical Failure' quest.
/// </summary>
internal sealed class Mount128Module : ICombatModule
{
    public const ushort MountId = 128;
    private readonly EAction[] _actions = [EAction.MagitekThunder, EAction.MagitekPulse];

    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;


    public Mount128Module(MovementController movementController, GameFunctions gameFunctions)
    {
        _movementController = movementController;
        _gameFunctions = gameFunctions;
    }

    public bool IsLoaded => _gameFunctions.GetMountId() == MountId;

    public bool Start() => true;

    public bool Stop() => true;

    public void Update(IGameObject gameObject)
    {
        if (_movementController.IsPathfinding || _movementController.IsPathRunning)
            return;

        foreach (EAction action in _actions)
        {
            if (_gameFunctions.UseAction(gameObject, action, checkCanUse: false))
                return;
        }
    }

    public void MoveToTarget(IGameObject gameObject)
    {
    }

    public bool CanAttack(IBattleNpc target) => target.DataId is 7504 or 7505 or 14107;
}
