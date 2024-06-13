namespace Questionable.Controller.Steps;

internal interface ILastTask : ITask
{
    public ushort QuestId { get; }
    public int Sequence { get; }
}
