using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.External;
using Questionable.Model.V1;
using Questionable.Model.V1.Converter;

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
    public DestinationData? Destination { get; private set; }

    public void Update()
    {
        if (_pathfindTask != null && Destination != null)
        {
            if (_pathfindTask.IsCompletedSuccessfully)
            {
                _pluginLog.Information(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Pathfinding complete, route: [{string.Join(" → ", _pathfindTask.Result.Select(x => x.ToString()))}]"));

                var navPoints = _pathfindTask.Result.Skip(1).ToList();
                Vector3 start = _clientState.LocalPlayer?.Position ?? navPoints[0];
                if (Destination.IsFlying && !_condition[ConditionFlag.InFlight] && _condition[ConditionFlag.Mounted])
                {
                    if (IsOnFlightPath(start) || navPoints.Any(IsOnFlightPath))
                    {
                        unsafe
                        {
                            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                        }
                    }
                }
                else if (!Destination.IsFlying && !_condition[ConditionFlag.Mounted] && navPoints.Count > 0 &&
                         !_gameFunctions.HasStatusPreventingSprintOrMount())
                {
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
            if ((localPlayerPosition - Destination.Position).Length() < Destination.StopDistance)
            {
                if (Destination.DataId is 2012173 or 2012174 or 2012175 or 2012176)
                {
                    Stop();
                }
                else if (Destination.DataId != null)
                {
                    GameObject? gameObject = _gameFunctions.FindObjectByDataId(Destination.DataId.Value);
                    if (gameObject is Character or EventObj)
                    {
                        if (Math.Abs(localPlayerPosition.Y - gameObject.Position.Y) < 1.95f)
                            Stop();
                    }
                    else if (gameObject != null && gameObject.ObjectKind == ObjectKind.Aetheryte)
                    {
                        if (AetheryteConverter.IsLargeAetheryte((EAetheryteLocation)Destination.DataId))
                        {
                            // TODO verify this
                            if (Math.Abs(localPlayerPosition.Y - gameObject.Position.Y) < 2.95f)
                                Stop();
                        }
                        else
                        {
                            // aethernet shard
                            if (Math.Abs(localPlayerPosition.Y - gameObject.Position.Y) < 1.95f)
                                Stop();
                        }
                    }
                    else
                        Stop();
                }
                else
                    Stop();
            }
        }
    }

    private bool IsOnFlightPath(Vector3 p)
    {
        Vector3? pointOnFloor = _navmeshIpc.GetPointOnFloor(p);
        return pointOnFloor != null && Math.Abs(pointOnFloor.Value.Y - p.Y) > 0.5f;
    }

    private void PrepareNavigation(EMovementType type, uint? dataId, Vector3 to, bool fly, float? stopDistance)
    {
        ResetPathfinding();

        _gameFunctions.ExecuteCommand("/automove off");

        Destination = new DestinationData(dataId, to, stopDistance ?? (DefaultStopDistance - 0.2f), fly);
    }

    public void NavigateTo(EMovementType type, uint? dataId, Vector3 to, bool fly, float? stopDistance = null)
    {
        PrepareNavigation(type, dataId, to, fly, stopDistance);
        _cancellationTokenSource = new();
        _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
        _pathfindTask =
            _navmeshIpc.Pathfind(_clientState.LocalPlayer!.Position, to, fly, _cancellationTokenSource.Token);
    }

    public void NavigateTo(EMovementType type, uint? dataId, List<Vector3> to, bool fly, float? stopDistance)
    {
        PrepareNavigation(type, dataId, to.Last(), fly, stopDistance);
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

    public sealed record DestinationData(uint? DataId, Vector3 Position, float StopDistance, bool IsFlying);
}
