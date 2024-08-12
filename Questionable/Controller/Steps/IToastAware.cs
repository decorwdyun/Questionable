using Dalamud.Game.Text.SeStringHandling;

namespace Questionable.Controller.Steps;

public interface IToastAware
{
    bool OnErrorToast(SeString message);
}
