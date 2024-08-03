using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal interface ILastTask : ITask
{
    public ElementId QuestElementId { get; }
    public int Sequence { get; }
}
