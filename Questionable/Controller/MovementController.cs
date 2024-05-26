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
    }

    public void NavigateTo(EMovementType type, Vector3 to, bool fly)
    {
        ResetPathfinding();

        _gameFunctions.ExecuteCommand("/automove off");

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
