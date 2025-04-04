﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Interactions;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps;

internal sealed class TaskCreator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TerritoryData _territoryData;
    private readonly IClientState _clientState;
    private readonly ILogger<TaskCreator> _logger;

    public TaskCreator(
        IServiceProvider serviceProvider,
        TerritoryData territoryData,
        IClientState clientState,
        ILogger<TaskCreator> logger)
    {
        _serviceProvider = serviceProvider;
        _territoryData = territoryData;
        _clientState = clientState;
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

        var singlePlayerDutyTask = newTasks
            .Where(y => y is SinglePlayerDuty.StartSinglePlayerDuty)
            .Cast<SinglePlayerDuty.StartSinglePlayerDuty>()
            .FirstOrDefault();
        if (singlePlayerDutyTask != null &&
            _territoryData.TryGetContentFinderCondition(singlePlayerDutyTask.ContentFinderConditionId,
                out var cfcData))
        {
            // if we have a single player duty in queue, we check if we're in the matching territory
            // if yes, skip all steps before (e.g. teleporting, waiting for navmesh, moving, interacting)
            if (_clientState.TerritoryType == cfcData.TerritoryId)
            {
                int index = newTasks.IndexOf(singlePlayerDutyTask);
                _logger.LogWarning(
                    "Skipping {SkippedTaskCount} out of {TotalCount} tasks, questionable was started while in single player duty",
                    index + 1, newTasks.Count);

                newTasks.RemoveRange(0, index + 1);
                _logger.LogInformation("Next actual task: {NextTask}, total tasks left: {RemainingTaskCount}",
                    newTasks.FirstOrDefault(),
                    newTasks.Count);
            }
        }

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
