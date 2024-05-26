using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;
using Questionable.Model.V1;

namespace Questionable.Controller;

internal sealed class QuestController
{
    private readonly Dictionary<ushort, Quest> _quests = new();

    public QuestController(DalamudPluginInterface pluginInterface)
    {
#if false
        LoadFromEmbeddedResources();
#endif
        LoadFromDirectory(new DirectoryInfo(@"E:\ffxiv\Questionable\Questionable\QuestPaths"));
        LoadFromDirectory(pluginInterface.ConfigDirectory);
    }

#if false
    private void LoadFromEmbeddedResources()
    {
        foreach (string resourceName in typeof(Questionable).Assembly.GetManifestResourceNames())
        {
            if (resourceName.EndsWith(".json"))
            {
                var (questId, name) = ExtractQuestDataFromName(resourceName);
                Quest quest = new Quest
                {
                    QuestId = questId,
                    Name = name,
                    Data = JsonSerializer.Deserialize<QuestData>(
                        typeof(Questionable).Assembly.GetManifestResourceStream(resourceName)!)!,
                };
                _quests[questId] = quest;
            }
        }
    }
#endif

    private void LoadFromDirectory(DirectoryInfo configDirectory)
    {
        foreach (FileInfo fileInfo in configDirectory.GetFiles("*.json"))
        {
            using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            var (questId, name) = ExtractQuestDataFromName(fileInfo.Name);
            Quest quest = new Quest
            {
                FilePath = fileInfo.FullName,
                QuestId = questId,
                Name = name,
                Data = JsonSerializer.Deserialize<QuestData>(stream)!,
            };
            _quests[questId] = quest;
        }

        foreach (DirectoryInfo childDirectory in configDirectory.GetDirectories())
            LoadFromDirectory(childDirectory);
    }

    private static (ushort QuestId, string Name) ExtractQuestDataFromName(string resourceName)
    {
        string name = resourceName.Substring(0, resourceName.Length - ".json".Length);
        name = name.Substring(name.LastIndexOf('.') + 1);

        ushort questId = ushort.Parse(name.Substring(0, name.IndexOf('_')));
        return (questId, name);
    }
}
