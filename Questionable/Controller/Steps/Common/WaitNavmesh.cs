namespace Questionable.Controller.Steps.Common;

internal sealed class WaitNavmesh
{
    internal sealed record Task : ITask
    {
        public override string ToString() => "等待(navmesh 就绪)";
    }

    internal sealed class Executor(MovementController movementController) : TaskExecutor<Task>, IDebugStateProvider
    {
        protected override bool Start() => true;

        public override ETaskResult Update() =>
            movementController.IsNavmeshReady ? ETaskResult.TaskComplete : ETaskResult.StillRunning;

        public override bool ShouldInterruptOnDamage() => false;

        public string? GetDebugState()
        {
            if (!movementController.IsNavmeshReady)
                return $"导航构建进度: {movementController.BuiltNavmeshPercent}%";
            else
                return null;
        }
    }
}
