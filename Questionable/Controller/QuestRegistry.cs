using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Utils;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestRegistry
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestData _questData;
    private readonly IChatGui _chatGui;
    private readonly ILogger<QuestRegistry> _logger;

    private readonly Dictionary<ushort, Quest> _quests = new();

    public QuestRegistry(IDalamudPluginInterface pluginInterface, QuestData questData, IChatGui chatGui,
        ILogger<QuestRegistry> logger)
    {
        _pluginInterface = pluginInterface;
        _questData = questData;
        _chatGui = chatGui;
        _logger = logger;
    }

    public IEnumerable<Quest> AllQuests => _quests.Values;
    public int Count => _quests.Count;

    public void Reload()
    {
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
        _logger.LogInformation("Loaded {Count} quests", _quests.Count);
    }

    [Conditional("RELEASE")]
    private void LoadQuestsFromAssembly()
    {
        _logger.LogInformation("Loading quests from assembly");

        foreach ((ushort questId, QuestRoot questRoot) in QuestPaths.AssemblyQuestLoader.GetQuests())
        {
            Quest quest = new()
            {
                QuestId = questId,
                Root = questRoot,
                Info = _questData.GetQuestInfo(questId),
            };
            _quests[questId] = quest;
        }
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
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "2.x - A Realm Reborn")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "5.x - Shadowbringers")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "6.x - Endwalker")));
                    LoadFromDirectory(
                        new DirectoryInfo(Path.Combine(pathProjectDirectory.FullName, "7.x - Dawntrail")));
                }
                catch (Exception e)
                {
                    _quests.Clear();
                    _logger.LogError(e, "Failed to load quests from project directory");
                }
            }
        }
    }

    [Conditional("DEBUG")]
    private void ValidateQuests()
    {
        Task.Run(() =>
        {
            try
            {
                int foundProblems = 0;
                foreach (var quest in _quests.Values)
                {
                    int missingSteps = quest.Root.QuestSequence.Where(x => x.Sequence < 255).Max(x => x.Sequence) -
                        quest.Root.QuestSequence.Count(x => x.Sequence < 255) + 1;
                    if (missingSteps != 0)
                    {
                        _logger.LogWarning("Quest has missing steps: {QuestId} / {QuestName} → {Count}", quest.QuestId,
                            quest.Info.Name, missingSteps);
                        ++foundProblems;
                    }

                    var totalSequenceCount = quest.Root.QuestSequence.Count;
                    var distinctSequenceCount = quest.Root.QuestSequence.Select(x => x.Sequence).Distinct().Count();
                    if (totalSequenceCount != distinctSequenceCount)
                    {
                        _logger.LogWarning("Quest has duplicate sequence numbers: {QuestId} / {QuestName}",
                            quest.QuestId,
                            quest.Info.Name);
                        ++foundProblems;
                    }

                    foreach (var sequence in quest.Root.QuestSequence)
                    {
                        if (sequence.Sequence == 0 &&
                            sequence.Steps.LastOrDefault()?.InteractionType != EInteractionType.AcceptQuest)
                        {
                            _logger.LogWarning(
                                "Quest likely has AcceptQuest configured wrong: {QuestId} / {QuestName} → {Sequence} / {Step}",
                                quest.QuestId, quest.Info.Name, sequence.Sequence, sequence.Steps.Count - 1);
                            ++foundProblems;
                        }
                        else if (sequence.Sequence == 255 &&
                                 sequence.Steps.LastOrDefault()?.InteractionType != EInteractionType.CompleteQuest)
                        {
                            _logger.LogWarning(
                                "Quest likely has CompleteQuest configured wrong: {QuestId} / {QuestName} → {Sequence} / {Step}",
                                quest.QuestId, quest.Info.Name, sequence.Sequence, sequence.Steps.Count - 1);
                            ++foundProblems;
                        }


                        var acceptQuestSteps = sequence.Steps
                            .Where(x => x is { InteractionType: EInteractionType.AcceptQuest, PickupQuestId: null })
                            .Where(x => sequence.Sequence != 0 || x != sequence.Steps.Last());
                        foreach (var step in acceptQuestSteps)
                        {
                            _logger.LogWarning(
                                "Quest has unexpected AcceptQuest steps: {QuestId} / {QuestName} → {Sequence} / {Step}",
                                quest.QuestId, quest.Info.Name, sequence.Sequence, sequence.Steps.IndexOf(step));
                            ++foundProblems;
                        }

                        var completeQuestSteps = sequence.Steps
                            .Where(x => x is { InteractionType: EInteractionType.CompleteQuest, TurnInQuestId: null })
                            .Where(x => sequence.Sequence != 255 || x != sequence.Steps.Last());
                        foreach (var step in completeQuestSteps)
                        {
                            _logger.LogWarning(
                                "Quest has unexpected CompleteQuest steps: {QuestId} / {QuestName} → {Sequence} / {Step}",
                                quest.QuestId, quest.Info.Name, sequence.Sequence, sequence.Steps.IndexOf(step));
                            ++foundProblems;
                        }

                        var completionFlags = sequence.Steps.Select(x => x.CompletionQuestVariablesFlags)
                            .Where(QuestWorkUtils.HasCompletionFlags)
                            .GroupBy(x =>
                            {
                                return Enumerable.Range(0, 6).Select(y =>
                                    {
                                        short? value = x[y];
                                        if (value == null || value.Value < 0)
                                            return (long)0;
                                        return (long)BitOperations.RotateLeft((ulong)value.Value, 8 * y);
                                    })
                                    .Sum();
                            })
                            .Where(x => x.Key != 0)
                            .Where(x => x.Count() > 1);
                        foreach (var duplicate in completionFlags)
                        {
                            _logger.LogWarning(
                                "Quest step has duplicate completion flags: {QuestId} / {QuestName} → {Sequence} → {Flags}",
                                quest.QuestId, quest.Info.Name, sequence.Sequence,
                                string.Join(", ", duplicate.First()));
                            ++foundProblems;
                        }
                    }
                }

                if (foundProblems > 0)
                {
                    _chatGui.Print(
                        $"[Questionable] Quest validation has found {foundProblems} problems. Check the log for details.");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to validate quests");
                _chatGui.PrintError(
                    $"[Questionable] Unable to validate quests. Check the log for details.");
            }
        });
    }


    private void LoadQuestFromStream(string fileName, Stream stream)
    {
        _logger.LogTrace("Loading quest from '{FileName}'", fileName);
        ushort? questId = ExtractQuestIdFromName(fileName);
        if (questId == null)
            return;

        Quest quest = new Quest
        {
            QuestId = questId.Value,
            Root = JsonSerializer.Deserialize<QuestRoot>(stream)!,
            Info = _questData.GetQuestInfo(questId.Value),
        };
        if (quest.Root.Disabled)
        {
            _logger.LogWarning("Quest {QuestId} / {QuestName} is disabled and won't be loaded", questId,
                quest.Info.Name);
            return;
        }

        _quests[questId.Value] = quest;
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

    private static ushort? ExtractQuestIdFromName(string resourceName)
    {
        string name = resourceName.Substring(0, resourceName.Length - ".json".Length);
        name = name.Substring(name.LastIndexOf('.') + 1);

        if (!name.Contains('_', StringComparison.Ordinal))
            return null;

        string[] parts = name.Split('_', 2);
        return ushort.Parse(parts[0], CultureInfo.InvariantCulture);
    }

    public bool IsKnownQuest(ushort questId) => _quests.ContainsKey(questId);

    public bool TryGetQuest(ushort questId, [NotNullWhen(true)] out Quest? quest)
        => _quests.TryGetValue(questId, out quest);
}
