using System.Collections.Generic;
using Questionable.Model.V1;

namespace Questionable.QuestPaths;

public static partial class AssemblyQuestLoader
{
    public static IReadOnlyDictionary<ushort, QuestRoot> GetQuests() =>
#if RELEASE
        Quests;
#else
        new Dictionary<ushort, QuestRoot>();
#endif
}
