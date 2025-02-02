using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal sealed class TaskCreator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskCreator> _logger;

    public TaskCreator(IServiceProvider serviceProvider, ILogger<TaskCreator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IReadOnlyList<ITask> CreateTasks(Quest quest, QuestSequence sequence, QuestStep step)
    {
        using var scope = _serviceProvider.CreateScope();
        var newTasks = scope.ServiceProvider.GetRequiredService<IEnumerable<ITaskFactory>>()
            .SelectMany(x =>
            {
                List<ITask> tasks = x.CreateAllTasks(quest, sequence, step).ToList();

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
