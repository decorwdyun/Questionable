using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class WaitGather(ICondition condition) : ITask
{
    private bool _wasGathering;

    public bool Start() => true;

    public ETaskResult Update()
    {
        if (condition[ConditionFlag.Gathering])
        {
            _wasGathering = true;
        }

        return _wasGathering && !condition[ConditionFlag.Gathering]
            ? ETaskResult.TaskComplete
            : ETaskResult.StillRunning;
    }

    public override string ToString() => "WaitGather";
}
