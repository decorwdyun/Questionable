using Dalamud.Game.ClientState.Objects.Types;

namespace Questionable.Controller.CombatModules;

internal interface ICombatModule
{
    bool IsLoaded { get; }

    bool Start();

    bool Stop();

    void Update(IGameObject nextTarget);

    void MoveToTarget(IGameObject nextTarget);
}
