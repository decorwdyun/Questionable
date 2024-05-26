using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Questionable.External;

namespace Questionable.Controller;

internal sealed class MovementController : IDisposable
{
    public const float DefaultStopDistance = 3f;
    private readonly NavmeshIpc _navmeshIpc;
    private readonly IClientState _clientState;
    private readonly GameFunctions _gameFunctions;
    private readonly IPluginLog _pluginLog;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task<List<Vector3>>? _pathfindTask;

    public MovementController(NavmeshIpc navmeshIpc, IClientState clientState, GameFunctions gameFunctions,
        IPluginLog pluginLog)
    {
        _navmeshIpc = navmeshIpc;
        _clientState = clientState;
        _gameFunctions = gameFunctions;
        _pluginLog = pluginLog;
    }

    public bool IsNavmeshReady => _navmeshIpc.IsReady;
    public bool IsPathRunning => _navmeshIpc.IsPathRunning;
    public bool IsPathfinding => _pathfindTask is { IsCompleted: false };
    public Vector3? Destination { get; private set; }
    public float StopDistance { get; private set; }

    public void Update()
    {
        if (_pathfindTask != null)
        {
            if (_pathfindTask.IsCompletedSuccessfully)
            {
                _pluginLog.Information(
                    $"Pathfinding complete, route: [{string.Join(" → ", _pathfindTask.Result.Select(x => x.ToString()))}]");
                _navmeshIpc.MoveTo(_pathfindTask.Result.Skip(1).ToList());
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

    public void NavigateTo(EMovementType type, Vector3 to, bool fly, float? stopDistance = null)
    {
        ResetPathfinding();


        _gameFunctions.ExecuteCommand("/automove off");

        Destination = to;
        StopDistance = stopDistance ?? (DefaultStopDistance - 0.2f);
        _cancellationTokenSource = new();
        _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
        _pathfindTask =
            _navmeshIpc.Pathfind(_clientState.LocalPlayer!.Position, to, fly, _cancellationTokenSource.Token);
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
