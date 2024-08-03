using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal interface ILastTask : ITask
{
    public IId QuestId { get; }
    public int Sequence { get; }
}
