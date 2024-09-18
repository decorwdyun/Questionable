namespace Questionable.Controller.Steps;

internal interface IRevisitAware : ITask
{
    void OnRevisit();
}
