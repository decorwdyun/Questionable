using System.Threading;
using System.Threading.Tasks;

namespace Questionable.Controller.Steps;

internal interface ITask
{
    bool Start();

    ETaskResult Update();
}
