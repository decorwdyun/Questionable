﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Questionable.Model.V1;

namespace Questionable.QuestPaths;

[SuppressMessage("ReSharper", "PartialTypeWithSinglePart", Justification = "Required for RELEASE")]
public static partial class AssemblyQuestLoader
{
    private static Dictionary<ushort, QuestRoot>? _quests;

    public static IReadOnlyDictionary<ushort, QuestRoot> GetQuests()
    {
        if (_quests == null)
        {
            _quests = [];
#if RELEASE
            LoadQuests();
#endif
        }

        return _quests ?? throw new InvalidOperationException("quest data is not initialized");
    }

    public static Stream QuestSchema =>
        typeof(AssemblyQuestLoader).Assembly.GetManifestResourceStream("Questionable.QuestPaths.QuestSchema")!;

    private static void AddQuest(ushort questId, QuestRoot root) => _quests![questId] = root;
}
