using System.Collections.Generic;
using Questionable.Model.V1;

#if RELEASE
namespace Questionable.QuestPaths;

public static partial class AssemblyQuestLoader
{
    public static IReadOnlyDictionary<ushort, QuestData> GetQuests() => Quests;
}
#endif
