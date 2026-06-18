using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Interactions;
using Questionable.Controller.Steps.Shared;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;
namespace Questionable.Controller.Steps;

internal sealed class TaskCreator
(
    IServiceProvider serviceProvider,
    TerritoryData territoryData,
    IClientState clientState,
    IChatGui chatGui,
    Configuration configuration,
    ILogger<TaskCreator> logger)
{
    private readonly IChatGui _chatGui = chatGui;
    private readonly IClientState _clientState = clientState;
    private readonly ILogger<TaskCreator> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly TerritoryData _territoryData = territoryData;

    public IReadOnlyList<ITask> CreateTasks(Quest quest, byte sequenceNumber, QuestSequence? sequence, QuestStep? step)
    {
        List<ITask> newTasks;

        if (!configuration.Advanced.Debug && quest.Root.Disabled && sequenceNumber.Equals(1))
        {
            var reason = (quest.Root.Comment ?? "<no reason specified>").Split('\n', 2)[0];
            _chatGui.PrintError($"任务'{quest.Info.Name}'已因以下原因被标记为已禁用：{reason}",
                CommandHandler.MessageTag, CommandHandler.TagColor);
            _chatGui.PrintError("我们建议您手动完成此任务，因为提供的路径可能无法成功运行。",
                CommandHandler.MessageTag, CommandHandler.TagColor);
            _chatGui.PrintError("感谢您的耐心等待，我们将在未来的更新中扩展QST的支持范围以纳入此任务。",
                CommandHandler.MessageTag, CommandHandler.TagColor);
        }

        if (sequence == null)
        {
            if (!quest.Root.Disabled &&
                quest.FindSequence((byte)(sequenceNumber - 1)) is { } prevSequence &&
                !prevSequence.Steps.Any(_step => _step is { InteractionType: EInteractionType.Duty or EInteractionType.SinglePlayerDuty }))
            {
                _chatGui.PrintError(
                    $"任务 '{quest.Info.Name}' ({quest.Id}) 的路径中没有找到序列 {sequenceNumber}，请在此报告问题：https://github.com/PunishXIV/Questionable/discussions/20",
                    CommandHandler.MessageTag, CommandHandler.TagColor);
            }

            newTasks = [new WaitAtEnd.WaitNextStepOrSequence()];
        }
        else if (step == null)
            newTasks = [new WaitAtEnd.WaitNextStepOrSequence()];
        else
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            newTasks = scope.ServiceProvider.GetRequiredService<IEnumerable<ITaskFactory>>()
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

            SinglePlayerDuty.StartSinglePlayerDuty? singlePlayerDutyTask = newTasks
                .Where(y => y is SinglePlayerDuty.StartSinglePlayerDuty)
                .Cast<SinglePlayerDuty.StartSinglePlayerDuty>()
                .FirstOrDefault();
            if (singlePlayerDutyTask != null &&
                _territoryData.TryGetContentFinderCondition(singlePlayerDutyTask.ContentFinderConditionId,
                    out TerritoryData.ContentFinderConditionData? cfcData))
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
        }

        if (newTasks.Count == 0)
            _logger.LogInformation("Nothing to execute for step?");
        else
        {
            _logger.LogInformation("Tasks for {QuestId}, {Sequence}, {Step}: {Tasks}",
                quest.Id, sequenceNumber, step != null ? sequence?.Steps.IndexOf(step) : null,
                string.Join(", ", newTasks.Select(x => x.ToString())));
        }

        return newTasks;
    }
}
