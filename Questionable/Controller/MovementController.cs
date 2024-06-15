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
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Microsoft.Extensions.Logging;
using Questionable.External;
using Questionable.Model;
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
    private readonly ILogger<MovementController> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task<List<Vector3>>? _pathfindTask;

    public MovementController(NavmeshIpc navmeshIpc, IClientState clientState, GameFunctions gameFunctions,
        ICondition condition, ILogger<MovementController> logger)
    {
        _navmeshIpc = navmeshIpc;
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _condition = condition;
        _logger = logger;
    }

    public bool IsNavmeshReady => _navmeshIpc.IsReady;
    public bool IsPathRunning => _navmeshIpc.IsPathRunning;
    public bool IsPathfinding => _pathfindTask is { IsCompleted: false };
    public DestinationData? Destination { get; set; }
    public DateTime MovementStartedAt { get; private set; } = DateTime.MaxValue;

    public void Update()
    {
        if (_pathfindTask != null && Destination != null)
        {
            if (_pathfindTask.IsCompletedSuccessfully)
            {
                _logger.LogInformation("Pathfinding complete, route: [{Route}]",
                    string.Join(" → ",
                        _pathfindTask.Result.Select(x => x.ToString("G", CultureInfo.InvariantCulture))));

                if (_pathfindTask.Result.Count == 0)
                {
                    ResetPathfinding();
                    throw new PathfindingFailedException();
                }

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

                _navmeshIpc.MoveTo(navPoints, Destination.IsFlying);
                MovementStartedAt = DateTime.Now;

                ResetPathfinding();
            }
            else if (_pathfindTask.IsCompleted)
            {
                _logger.LogWarning("Unable to complete pathfinding task");
                ResetPathfinding();
                throw new PathfindingFailedException();
            }
        }

        if (IsPathRunning && Destination != null)
        {
            if (_gameFunctions.IsLoadingScreenVisible())
            {
                Stop();
                return;
            }

            if (Destination is { IsFlying: true } && _condition[ConditionFlag.Swimming])
            {
                _logger.LogInformation("Flying but swimming, restarting as non-flying path...");
                var dest = Destination;
                Stop();

                if (dest.UseNavmesh)
                    NavigateTo(EMovementType.None, dest.DataId, dest.Position, false, false, dest.StopDistance);
                else
                    NavigateTo(EMovementType.None, dest.DataId, [dest.Position], false, false, dest.StopDistance);
                return;
            }

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
                            /*
                            if ((EAetheryteLocation) Destination.DataId is EAetheryteLocation.OldSharlayan
                                or EAetheryteLocation.UltimaThuleAbodeOfTheEa)
                                Stop();

                            // TODO verify the first part of this, is there any aetheryte like that?
                            // TODO Unsure if this is per-aetheryte or what; because e.g. old sharlayan is at -1.53;
                            //      but Elpis aetherytes fail at around -0.95
                            if (localPlayerPosition.Y - gameObject.Position.Y < 2.95f &&
                                localPlayerPosition.Y - gameObject.Position.Y > -0.9f)
                                Stop();
                            */
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
            else if (!Destination.IsFlying && !_condition[ConditionFlag.Mounted] &&
                     !_gameFunctions.HasStatusPreventingSprint() && Destination.CanSprint)
            {
                List<Vector3> navPoints = _navmeshIpc.GetWaypoints();
                Vector3? start = _clientState.LocalPlayer?.Position;
                if (start != null)
                {
                    float actualDistance = 0;
                    foreach (Vector3 end in navPoints)
                    {
                        actualDistance += (start.Value - end).Length();
                        start = end;
                    }

                    unsafe
                    {
                        // 70 is ~10 seconds of sprint
                        if (actualDistance > 100f &&
                            ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 4) == 0)
                        {
                            _logger.LogInformation("Triggering Sprint");
                            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);
                        }
                    }
                }
            }
        }
    }

    private bool IsOnFlightPath(Vector3 p)
    {
        Vector3? pointOnFloor = _navmeshIpc.GetPointOnFloor(p);
        return pointOnFloor != null && Math.Abs(pointOnFloor.Value.Y - p.Y) > 0.5f;
    }

    private void PrepareNavigation(EMovementType type, uint? dataId, Vector3 to, bool fly, bool sprint,
        float? stopDistance, bool useNavmesh)
    {
        ResetPathfinding();

        if (InputManager.IsAutoRunning())
        {
            _logger.LogInformation("Turning off auto-move");
            _gameFunctions.ExecuteCommand("/automove off");
        }

        Destination = new DestinationData(dataId, to, stopDistance ?? (DefaultStopDistance - 0.2f), fly, sprint,
            useNavmesh);
        MovementStartedAt = DateTime.MaxValue;
    }

    public void NavigateTo(EMovementType type, uint? dataId, Vector3 to, bool fly, bool sprint,
        float? stopDistance = null)
    {
        fly |= _condition[ConditionFlag.Diving];
        PrepareNavigation(type, dataId, to, fly, sprint, stopDistance, true);
        _logger.LogInformation("Pathfinding to {Destination}", Destination);

        _cancellationTokenSource = new();
        _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));
        _pathfindTask =
            _navmeshIpc.Pathfind(_clientState.LocalPlayer!.Position, to, fly, _cancellationTokenSource.Token);
    }

    public void NavigateTo(EMovementType type, uint? dataId, List<Vector3> to, bool fly, bool sprint,
        float? stopDistance)
    {
        fly |= _condition[ConditionFlag.Diving];
        PrepareNavigation(type, dataId, to.Last(), fly, sprint, stopDistance, false);

        _logger.LogInformation("Moving to {Destination}", Destination);
        _navmeshIpc.MoveTo(to, fly);
        MovementStartedAt = DateTime.Now;
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

        if (InputManager.IsAutoRunning())
        {
            _logger.LogInformation("Turning off auto-move [stop]");
            _gameFunctions.ExecuteCommand("/automove off");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public sealed record DestinationData(
        uint? DataId,
        Vector3 Position,
        float StopDistance,
        bool IsFlying,
        bool CanSprint,
        bool UseNavmesh);

    public sealed class PathfindingFailedException : Exception
    {
        public PathfindingFailedException()
        {
        }

        public PathfindingFailedException(string message)
            : base(message)
        {
        }

        public PathfindingFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
