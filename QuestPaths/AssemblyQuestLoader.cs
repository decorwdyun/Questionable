using System;
using System.IO;
using System.IO.Compression;

namespace Questionable.QuestPaths;

public static class AssemblyQuestLoader
{
    public static void LoadQuestsFromEmbeddedResources(Action<string, Stream> loadFunction)
    {
        foreach (string resourceName in typeof(AssemblyQuestLoader).Assembly.GetManifestResourceNames())
        {
            if (resourceName.EndsWith(".zip"))
            {
                using ZipArchive zipArchive =
                    new ZipArchive(typeof(AssemblyQuestLoader).Assembly.GetManifestResourceStream(resourceName)!);
                foreach (ZipArchiveEntry entry in zipArchive.Entries)
                {
                    using Stream stream = entry.Open();
                    loadFunction(entry.Name, stream);
                }
            }
        }
    }
}
