using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
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
    private readonly ICallGateProvider<object> _reloadDataIpc;

    private readonly Dictionary<ElementId, Quest> _quests = new();

    public QuestRegistry(IDalamudPluginInterface pluginInterface, QuestData questData,
        QuestValidator questValidator, JsonSchemaValidator jsonSchemaValidator,
        ILogger<QuestRegistry> logger)
    {
        _pluginInterface = pluginInterface;
        _questData = questData;
        _questValidator = questValidator;
        _jsonSchemaValidator = jsonSchemaValidator;
        _logger = logger;
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

        LoadQuestsFromAssembly();
        LoadQuestsFromProjectDirectory();

        try
        {
            LoadFromDirectory(new DirectoryInfo(Path.Combine(_pluginInterface.ConfigDirectory.FullName, "Quests")));
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Failed to load all quests from user directory (some may have been successfully loaded)");
        }

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

        foreach ((ushort questId, QuestRoot questRoot) in AssemblyQuestLoader.GetQuests())
        {
            Quest quest = new()
            {
                Id = new QuestId(questId),
                Root = questRoot,
                Info = _questData.GetQuestInfo(new QuestId(questId)),
                ReadOnly = true,
            };
            _quests[quest.Id] = quest;
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

    private void ValidateQuests()
    {
        _questValidator.Validate(_quests.Values.Where(x => !x.ReadOnly));
    }

    private void LoadQuestFromStream(string fileName, Stream stream)
    {
        _logger.LogTrace("Loading quest from '{FileName}'", fileName);
        ElementId? questId = ExtractQuestIdFromName(fileName);
        if (questId == null)
            return;

        var questNode = JsonNode.Parse(stream)!;
        _jsonSchemaValidator.Enqueue(questId, questNode);

        Quest quest = new Quest
        {
            Id = questId,
            Root = questNode.Deserialize<QuestRoot>()!,
            Info = _questData.GetQuestInfo(questId),
            ReadOnly = false,
        };
        _quests[quest.Id] = quest;
    }

    private void LoadFromDirectory(DirectoryInfo directory, LogLevel logLevel = LogLevel.Information)
    {
        if (!directory.Exists)
        {
            _logger.LogInformation("Not loading quests from {DirectoryName} (doesn't exist)", directory);
            return;
        }

        _logger.Log(logLevel, "Loading quests from {DirectoryName}", directory);
        foreach (FileInfo fileInfo in directory.GetFiles("*.json"))
        {
            try
            {
                using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                LoadQuestFromStream(fileInfo.Name, stream);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Unable to load file {fileInfo.FullName}", e);
            }
        }

        foreach (DirectoryInfo childDirectory in directory.GetDirectories())
            LoadFromDirectory(childDirectory, logLevel);
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
}
