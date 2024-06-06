using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Questionable.External;

internal sealed class NavmeshIpc
{
    private readonly ICallGateSubscriber<bool> _isNavReady;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> _navPathfind;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> _pathMoveTo;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _pathIsRunning;
    private readonly ICallGateSubscriber<float> _pathGetTolerance;
    private readonly ICallGateSubscriber<float, object> _pathSetTolerance;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _queryPointOnFloor;

    public NavmeshIpc(DalamudPluginInterface pluginInterface)
    {
        _isNavReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navPathfind =
            pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>>(
                $"vnavmesh.Nav.PathfindCancelable");
        _pathMoveTo = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        _pathStop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _pathIsRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathGetTolerance = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Path.GetTolerance");
        _pathSetTolerance = pluginInterface.GetIpcSubscriber<float, object>("vnavmesh.Path.SetTolerance");
        _queryPointOnFloor =
            pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
    }

    public bool IsReady
    {
        get
        {
            try
            {
                return _isNavReady.InvokeFunc();
            }
            catch (IpcError)
            {
                return false;
            }
        }
    }

    public bool IsPathRunning => _pathIsRunning.InvokeFunc();

    public void Stop() => _pathStop.InvokeAction();

    public Task<List<Vector3>> Pathfind(Vector3 localPlayerPosition, Vector3 targetPosition, bool fly,
        CancellationToken cancellationToken)
    {
        _pathSetTolerance.InvokeAction(0.25f);
        return _navPathfind.InvokeFunc(localPlayerPosition, targetPosition, fly, cancellationToken);
    }

    public void MoveTo(List<Vector3> position, bool fly)
    {
        Stop();

        _pathMoveTo.InvokeAction(position, fly);
    }

    public Vector3? GetPointOnFloor(Vector3 position)
        => _queryPointOnFloor.InvokeFunc(position, true, 1);
}
