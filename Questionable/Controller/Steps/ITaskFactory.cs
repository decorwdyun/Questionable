using System.Collections.Generic;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps;

internal interface ITaskFactory
{
    ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step);

    IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
    {
        var task = CreateTask(quest, sequence, step);
        if (task != null)
            yield return task;
    }
}
