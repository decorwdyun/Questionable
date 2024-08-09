using Dalamud.Game.ClientState.Conditions;

namespace Questionable.Controller.Steps;

public interface IConditionChangeAware
{
    void OnConditionChange(ConditionFlag flag, bool value);
}
