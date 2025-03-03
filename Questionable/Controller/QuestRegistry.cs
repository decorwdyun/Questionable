﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using LLib.GameData;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.QuestPaths;
using Questionable.Validation;
using Questionable.Validation.Validators;

namespace Questionable.Controller;

internal sealed class QuestRegistry
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestData _questData;
    private readonly QuestValidator _questValidator;
    private readonly JsonSchemaValidator _jsonSchemaValidator;
    private readonly ILogger<QuestRegistry> _logger;
    private readonly LeveData _leveData;
    private readonly TerritoryData _territoryData;

    private readonly ICallGateProvider<object> _reloadDataIpc;
    private readonly Dictionary<ElementId, Quest> _quests = [];
    private readonly Dictionary<uint, (ElementId QuestId, QuestStep Step)> _contentFinderConditionIds = [];

    public QuestRegistry(IDalamudPluginInterface pluginInterface, QuestData questData,
        QuestValidator questValidator, JsonSchemaValidator jsonSchemaValidator,
        ILogger<QuestRegistry> logger, LeveData leveData, TerritoryData territoryData)
    {
        _pluginInterface = pluginInterface;
        _questData = questData;
        _questValidator = questValidator;
        _jsonSchemaValidator = jsonSchemaValidator;
        _logger = logger;
        _leveData = leveData;
        _territoryData = territoryData;
        _reloadDataIpc = _pluginInterface.GetIpcProvider<object>("Questionable.ReloadData");
    }

    public IEnumerable<Quest> AllQuests => _quests.Values;
    public int Count => _quests.Count(x => !x.Value.Root.Disabled);
    public int ValidationIssueCount => _questValidator.IssueCount;
    public int ValidationErrorCount => _questValidator.ErrorCount;

    public event EventHandler? Reloaded;

    public void Reload()
    {
        _questValidator.Reset();
        _quests.Clear();
        _contentFinderConditionIds.Clear();

        LoadQuestsFromAssembly();
        LoadQuestsFromProjectDirectory();

        try
        {
            LoadFromDirectory(new DirectoryInfo(Path.Combine(_pluginInterface.ConfigDirectory.FullName, "Quests")),
                Quest.ESource.UserDirectory);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Failed to load all quests from user directory (some may have been successfully loaded)");
        }

        LoadCfcIds();
        ValidateQuests();
        Reloaded?.Invoke(this, EventArgs.Empty);
        try
        {
            _reloadDataIpc.SendMessage();
        }
        catch (Exception e)
        {
            // why does this even throw
            _logger.LogWarning(e, "Error during Reload.SendMessage IPC");
        }

        _logger.LogInformation("Loaded {Count} quests in total", _quests.Count);
    }

    [Conditional("RELEASE")]
    private void LoadQuestsFromAssembly()
    {
        _logger.LogInformation("Loading quests from assembly");

        foreach ((ElementId questId, QuestRoot questRoot) in AssemblyQuestLoader.GetQuests())
        {
            try
            {
                var questInfo = _questData.GetQuestInfo(questId);
                if (questInfo is LeveInfo leveInfo)
                    _leveData.AddQuestSteps(leveInfo, questRoot);
                Quest quest = new()
                {
                    Id = questId,
                    Root = questRoot,
                    Info = questInfo,
                    Source = Quest.ESource.Assembly,
                };
                _quests[quest.Id] = quest;
            }
            catch (Exception e)
            {
                _logger.LogWarning("Not loading unknown quest {QuestId} from assembly: {Message}", questId, e.Message);
            }
        }

        _logger.LogInformation("Loaded {Count} quests from assembly", _quests.Count);
    }

    [Conditional("DEBUG")]
    private void LoadQuestsFromProjectDirectory()
    {
        DirectoryInfo? solutionDirectory = _pluginInterface.AssemblyLocation.Directory?.Parent?.Parent;
        if (solutionDirectory != null)
        {
            DirectoryInfo pathProjectDirectory =
                new DirectoryInfo(Path.Combine(solutionDirectory.FullName, "QuestPaths"));
            if (pathProjectDirectory.Exists)
            {
                try
                {
                    foreach (var expansionFolder in ExpansionData.ExpansionFolders.Values)
                        LoadFromDirectory(
                            new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, expansionFolder)),
                            Quest.ESource.ProjectDirectory,
                            LogLevel.Trace);
                }
                catch (Exception e)
                {
                    _quests.Clear();
                    _logger.LogError(e, "Failed to load quests from project directory");
                }
            }
        }
    }

    private void LoadCfcIds()
    {
        foreach (var quest in _quests.Values)
        {
            foreach (var dutyStep in quest.AllSteps().Where(x =>
                         x.Step.InteractionType is EInteractionType.Duty or EInteractionType.SinglePlayerDuty))
            {
                if (dutyStep.Step is { InteractionType: EInteractionType.Duty, ContentFinderConditionId: not null })
                    _contentFinderConditionIds[dutyStep.Step.ContentFinderConditionId!.Value] =
                        (quest.Id, dutyStep.Step);
                else if (dutyStep.Step.InteractionType == EInteractionType.SinglePlayerDuty &&
                         _territoryData.TryGetContentFinderConditionForSoloInstance(quest.Id,
                             dutyStep.Step.SinglePlayerDutyIndex, out var cfcData))
                    _contentFinderConditionIds[cfcData.ContentFinderConditionId] = (quest.Id, dutyStep.Step);
            }
        }
    }

    private void ValidateQuests()
    {
        _questValidator.Validate(_quests.Values.Where(x => x.Source != Quest.ESource.Assembly).ToList());
    }

    private void LoadQuestFromStream(string fileName, Stream stream, Quest.ESource source)
    {
        if (source == Quest.ESource.UserDirectory)
            _logger.LogTrace("Loading quest from '{FileName}'", fileName);
        ElementId? questId = ExtractQuestIdFromName(fileName);
        if (questId == null)
            return;

        var questNode = JsonNode.Parse(stream)!;
        _jsonSchemaValidator.Enqueue(questId, questNode);

        var questRoot = questNode.Deserialize<QuestRoot>()!;
        if (!_questData.TryGetQuestInfo(questId, out var questInfo))
        {
            _logger.LogWarning("Not loading unknown quest {QuestId} from '{Filename}'", questId, fileName);
            return;
        }
        if (questInfo is LeveInfo leveInfo)
            _leveData.AddQuestSteps(leveInfo, questRoot);
        Quest quest = new Quest
        {
            Id = questId,
            Root = questRoot,
            Info = questInfo,
            Source = source,
        };
        _quests[quest.Id] = quest;
    }

    private void LoadFromDirectory(DirectoryInfo directory, Quest.ESource source,
        LogLevel logLevel = LogLevel.Information)
    {
        if (!directory.Exists)
        {
            _logger.LogInformation("Not loading quests from {DirectoryName} (doesn't exist)", directory);
            return;
        }

        if (source == Quest.ESource.UserDirectory)
            _logger.Log(logLevel, "Loading quests from {DirectoryName}", directory);
        foreach (FileInfo fileInfo in directory.GetFiles("*.json"))
        {
            try
            {
                using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                LoadQuestFromStream(fileInfo.Name, stream, source);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Unable to load file {fileInfo.FullName}", e);
            }
        }

        foreach (DirectoryInfo childDirectory in directory.GetDirectories())
            LoadFromDirectory(childDirectory, source, logLevel);
    }

    private static ElementId? ExtractQuestIdFromName(string resourceName)
    {
        string name = resourceName.Substring(0, resourceName.Length - ".json".Length);
        name = name.Substring(name.LastIndexOf('.') + 1);

        if (!name.Contains('_', StringComparison.Ordinal))
            return null;

        string[] parts = name.Split('_', 2);
        return ElementId.FromString(parts[0]);
    }

    public bool IsKnownQuest(ElementId questId) => _quests.ContainsKey(questId);

    public bool TryGetQuest(ElementId questId, [NotNullWhen(true)] out Quest? quest)
        => _quests.TryGetValue(questId, out quest);

    public List<QuestInfo> GetKnownClassJobQuests(EClassJob classJob, bool includeRoleQuests = true)
    {
        List<QuestInfo> allQuests = [.._questData.GetClassJobQuests(classJob, includeRoleQuests)];
        if (classJob.AsJob() != classJob)
            allQuests.AddRange(_questData.GetClassJobQuests(classJob.AsJob(), includeRoleQuests));

        return allQuests
            .Where(x => IsKnownQuest(x.QuestId))
            .ToList();
    }

    public bool TryGetDutyByContentFinderConditionId(uint cfcId, out bool autoDutyEnabledByDefault)
    {
        if (_contentFinderConditionIds.TryGetValue(cfcId, out var value))
        {
            autoDutyEnabledByDefault = value.Step.AutoDutyEnabled;
            return true;
        }

        autoDutyEnabledByDefault = false;
        return false;
    }
}
