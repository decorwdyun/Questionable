using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal sealed class TaskCreator
{
    private readonly IReadOnlyList<ITaskFactory> _taskFactories;
    private readonly ILogger<TaskCreator> _logger;

    public TaskCreator(IEnumerable<ITaskFactory> taskFactories, ILogger<TaskCreator> logger)
    {
        _taskFactories = taskFactories.ToList().AsReadOnly();
        _logger = logger;
    }

    public IReadOnlyList<ITask> CreateTasks(Quest quest, QuestSequence sequence, QuestStep step)
    {
        var newTasks = _taskFactories
            .SelectMany(x =>
            {
                IList<ITask> tasks = x.CreateAllTasks(quest, sequence, step).ToList();

                if (tasks.Count > 0 && _logger.IsEnabled(LogLevel.Trace))
                {
                    string factoryName = x.GetType().FullName ?? x.GetType().Name;
                    if (factoryName.Contains('.', StringComparison.Ordinal))
                        factoryName = factoryName[(factoryName.LastIndexOf('.') + 1)..];

                    _logger.LogTrace("Factory {FactoryName} created Task {TaskNames}",
                        factoryName, string.Join(", ", tasks.Select(y => y.ToString())));
                }

                return tasks;
            })
            .ToList();
        if (newTasks.Count == 0)
            _logger.LogInformation("Nothing to execute for step?");
        else
        {
            _logger.LogInformation("Tasks for {QuestId}, {Sequence}, {Step}: {Tasks}",
                quest.Id, sequence.Sequence, sequence.Steps.IndexOf(step),
                string.Join(", ", newTasks.Select(x => x.ToString())));
        }

        return newTasks;
    }
}
