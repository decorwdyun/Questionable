using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestRegistry
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestData _questData;
    private readonly ILogger<QuestRegistry> _logger;

    private readonly Dictionary<ushort, Quest> _quests = new();

    public QuestRegistry(IDalamudPluginInterface pluginInterface, QuestData questData,
        ILogger<QuestRegistry> logger)
    {
        _pluginInterface = pluginInterface;
        _questData = questData;
        _logger = logger;
    }

    public void Reload()
    {
        _quests.Clear();

#if RELEASE
        _logger.LogInformation("Loading quests from assembly");

        foreach ((ushort questId, QuestData questData) in QuestPaths.AssemblyQuestLoader.GetQuests())
        {
            Quest quest = new()
            {
                QuestId = questId,
                Name = string.Empty,
                Data = questData,
            };
            _quests[questId] = quest;
        }
#else
        DirectoryInfo? solutionDirectory = _pluginInterface.AssemblyLocation.Directory?.Parent?.Parent;
        if (solutionDirectory != null)
        {
            DirectoryInfo pathProjectDirectory =
                new DirectoryInfo(Path.Combine(solutionDirectory.FullName, "QuestPaths"));
            if (pathProjectDirectory.Exists)
            {
                LoadFromDirectory(new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "ARealmReborn")));
                LoadFromDirectory(new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "Shadowbringers")));
                LoadFromDirectory(new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "Endwalker")));
                LoadFromDirectory(new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "Dawntrail")));
            }
        }
#endif

        LoadFromDirectory(new DirectoryInfo(Path.Combine(_pluginInterface.ConfigDirectory.FullName, "Quests")));

#if !RELEASE
        foreach (var quest in _quests.Values)
        {
            int missingSteps = quest.Root.QuestSequence.Where(x => x.Sequence < 255).Max(x => x.Sequence) - quest.Root.QuestSequence.Count(x => x.Sequence < 255) + 1;
            if (missingSteps != 0)
                _logger.LogWarning("Quest has missing steps: {QuestId} / {QuestName} → {Count}", quest.QuestId, quest.Info.Name, missingSteps);
        }
#endif

        _logger.LogInformation("Loaded {Count} quests", _quests.Count);
    }


    private void LoadQuestFromStream(string fileName, Stream stream)
    {
        _logger.LogTrace("Loading quest from '{FileName}'", fileName);
        var questId = ExtractQuestIdFromName(fileName);
        Quest quest = new Quest
        {
            QuestId = questId,
            Root = JsonSerializer.Deserialize<QuestRoot>(stream)!,
            Info = _questData.GetQuestInfo(questId),
        };
        _quests[questId] = quest;
    }

    private void LoadFromDirectory(DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            _logger.LogInformation("Not loading quests from {DirectoryName} (doesn't exist)", directory);
            return;
        }

        _logger.LogInformation("Loading quests from {DirectoryName}", directory);
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
            LoadFromDirectory(childDirectory);
    }

    private static ushort ExtractQuestIdFromName(string resourceName)
    {
        string name = resourceName.Substring(0, resourceName.Length - ".json".Length);
        name = name.Substring(name.LastIndexOf('.') + 1);

        string[] parts = name.Split('_', 2);
        return ushort.Parse(parts[0], CultureInfo.InvariantCulture);
    }

    public bool IsKnownQuest(ushort questId) => _quests.ContainsKey(questId);

    public bool TryGetQuest(ushort questId, [NotNullWhen(true)] out Quest? quest)
        => _quests.TryGetValue(questId, out quest);
}
