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
internal sealed class Mount147Module : ICombatModule
{
    public const ushort MountId = 147;
    private readonly EAction[] _actions = [EAction.Trample];

    private readonly MovementController _movementController;
    private readonly GameFunctions _gameFunctions;


    public Mount147Module(MovementController movementController, GameFunctions gameFunctions)
    {
        _movementController = movementController;
        _gameFunctions = gameFunctions;
    }

    public bool CanHandleFight(CombatController.CombatData combatData) => _gameFunctions.GetMountId() == MountId;

    public bool Start(CombatController.CombatData combatData) => true;

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

    public bool CanAttack(IBattleNpc target) => target.DataId is 8593;
}
