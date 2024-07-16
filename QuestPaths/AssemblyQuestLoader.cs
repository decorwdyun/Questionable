using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Questionable.Model.V1;

namespace Questionable.QuestPaths;

[SuppressMessage("ReSharper", "PartialTypeWithSinglePart", Justification = "Required for RELEASE")]
public static partial class AssemblyQuestLoader
{
    public static IReadOnlyDictionary<ushort, QuestRoot> GetQuests() =>
#if RELEASE
        Quests;
#else
        new Dictionary<ushort, QuestRoot>();
#endif
}
