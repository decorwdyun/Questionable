using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.External;

namespace Questionable.Controller;

internal sealed class MovementController : IDisposable
{
    public const float DefaultStopDistance = 3f;
    private readonly NavmeshIpc _navmeshIpc;
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly ICondition _condition;
    private readonly IPluginLog _pluginLog;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task<List<Vector3>>? _pathfindTask;

    public MovementController(NavmeshIpc navmeshIpc, IClientState clientState, GameFunctions gameFunctions,
        ICondition condition, IPluginLog pluginLog)
    {
        _navmeshIpc = navmeshIpc;
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _condition = condition;
        _pluginLog = pluginLog;
    }

    public bool IsNavmeshReady => _navmeshIpc.IsReady;
    public bool IsPathRunning => _navmeshIpc.IsPathRunning;
    public bool IsPathfinding => _pathfindTask is { IsCompleted: false };
    public Vector3? Destination { get; private set; }
    public float StopDistance { get; private set; }
    public bool IsFlying { get; private set; }

    public void Update()
    {
        if (_pathfindTask != null)
        {
            if (_pathfindTask.IsCompletedSuccessfully)
            {
                _pluginLog.Information(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Pathfinding complete, route: [{string.Join(" → ", _pathfindTask.Result.Select(x => x.ToString()))}]"));

                var navPoints = _pathfindTask.Result.Skip(1).ToList();
                if (!IsFlying && !_condition[ConditionFlag.Mounted] && navPoints.Count > 0 &&
                    !_gameFunctions.HasStatusPreventingSprintOrMount())
                {
                    Vector3 start = _clientState.LocalPlayer?.Position ?? navPoints[0];
                    float actualDistance = 0;
                    foreach (Vector3 end in navPoints)
                    {
                        actualDistance += (start - end).Length();
                        start = end;
                    }

                    _pluginLog.Information($"Distance: {actualDistance}");
                    unsafe
                    {
                        // 70 is ~10 seconds of sprint
                        if (actualDistance > 100f &&
                            ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 4) == 0)
                        {
                            _pluginLog.Information("Triggering Sprint");
                            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);
                        }
                    }
                }

                _navmeshIpc.MoveTo(navPoints);
                ResetPathfinding();
            }
            else if (_pathfindTask.IsCompleted)
            {
                _pluginLog.Information("Unable to complete pathfinding task");
                ResetPathfinding();
            }
        }

        if (IsPathRunning && Destination != null)
        {
            Vector3 localPlayerPosition = _clientState.LocalPlayer?.Position ?? Vector3.Zero;
            if ((localPlayerPosition - Destination.Value).Length() < StopDistance)
                Stop();
        }
    }

    private void PrepareNavigation(EMovementType type, Vector3 to, bool fly, float? stopDistance)
    {
        ResetPathfinding();

        _gameFunctions.ExecuteCommand("/automove off");

        Destination = to;
        StopDistance = stopDistance ?? (DefaultStopDistance - 0.2f);
        IsFlying = fly;
    }

    public void NavigateTo(EMovementType type, Vector3 to, bool fly, float? stopDistance = null)
    {
        PrepareNavigation(type, to, fly, stopDistance);
        _cancellationTokenSource = new();
        _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
        _pathfindTask =
            _navmeshIpc.Pathfind(_clientState.LocalPlayer!.Position, to, fly, _cancellationTokenSource.Token);
    }

    public void NavigateTo(EMovementType type, List<Vector3> to, bool fly, float? stopDistance)
    {
        PrepareNavigation(type, to.Last(), fly, stopDistance);
        _navmeshIpc.MoveTo(to);
    }

    public void ResetPathfinding()
    {
        if (_cancellationTokenSource != null)
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        _pathfindTask = null;
    }

    public void Stop()
    {
        _navmeshIpc.Stop();
        ResetPathfinding();
    }

    public void Dispose()
    {
        Stop();
    }
}
