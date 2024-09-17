using System.Threading;
using System.Threading.Tasks;

namespace Questionable.Controller.Steps;

internal interface ITask
{
    InteractionProgressContext? ProgressContext() => null;

    bool WasInterrupted()
    {
        var progressContext = ProgressContext();
        if (progressContext != null)
        {
            progressContext.Update();
            return progressContext.WasInterrupted();
        }

        return false;
    }

    bool ShouldRedoOnInterrupt() => false;

    bool Start();

    ETaskResult Update();
}
