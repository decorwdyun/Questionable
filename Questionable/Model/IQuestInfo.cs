﻿using System;
using Dalamud.Game.Text;
using Questionable.Model.Questing;

namespace Questionable.Model;

public interface IQuestInfo
{
    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable { get; }
    public ushort Level { get; }
    public EBeastTribe BeastTribe { get; }
    public bool IsMainScenarioQuest { get; }

    public string SimplifiedName => Name
        .Replace(".", "", StringComparison.Ordinal)
        .TrimStart(SeIconChar.QuestSync.ToIconChar(), SeIconChar.QuestRepeatable.ToIconChar(), ' ');
}
