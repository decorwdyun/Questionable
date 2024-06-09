namespace Questionable.Controller.Steps;

internal enum ETaskResult
{
    StillRunning,

    TaskComplete,

    /// <summary>
    /// This step is complete, regardless of what any other following tasks would do.
    /// </summary>
    SkipRemainingTasksForStep,

    NextStep,
    End,
}
