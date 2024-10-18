using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Common.Math;
using LLib.GameData;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Data;

internal sealed class LeveData
{
    private static readonly List<LeveStepData> Leves =
    [
        new(
            aetheryteLocation: EAetheryteLocation.Crystarium,
            aethernetShortcut: new AethernetShortcut{From = EAetheryteLocation.Crystarium, To = EAetheryteLocation.CrystariumCrystallineMean},
            issuerDataId: 1027847,
            issuerPosition: new(-73.94349f, 19.999794f, -110.86395f)),
        new(
            aetheryteLocation: EAetheryteLocation.OldSharlayan,
            aethernetShortcut: new AethernetShortcut
                { From = EAetheryteLocation.OldSharlayan, To = EAetheryteLocation.OldSharlayanScholarsHarbor },
            issuerDataId: 1037263,
            issuerPosition: new(45.818386f, -15.646993f, 109.40509f)),
        new(aetheryteLocation: EAetheryteLocation.Tuliyollal,
            aethernetShortcut: null,
            issuerDataId: 1048390,
            issuerPosition: new(15.243713f, -14.000001f, 85.83191f)),
    ];

    private readonly AetheryteData _aetheryteData;

    public LeveData(AetheryteData aetheryteData)
    {
        _aetheryteData = aetheryteData;
    }

    public void AddQuestSteps(LeveInfo leveInfo, QuestRoot questRoot)
    {
        LeveStepData leveStepData = Leves.SingleOrDefault(x => x.IssuerDataId == leveInfo.IssuerDataId)
                                    ?? throw new InvalidOperationException(
                                        $"No leve location for issuer data id {leveInfo.IssuerDataId} found");

        QuestSequence? startSequence = questRoot.QuestSequence.FirstOrDefault(x => x.Sequence == 0);
        if (startSequence == null)
        {
            questRoot.QuestSequence.Add(new QuestSequence
            {
                Sequence = 0,
                Steps =
                [
                    new QuestStep
                    {
                        DataId = leveStepData.IssuerDataId,
                        Position = leveStepData.IssuerPosition,
                        TerritoryId = _aetheryteData.TerritoryIds[leveStepData.AetheryteLocation],
                        InteractionType = EInteractionType.AcceptLeve,
                        AetheryteShortcut = leveStepData.AetheryteLocation,
                        AethernetShortcut = leveStepData.AethernetShortcut,
                        SkipConditions = new()
                        {
                            AetheryteShortcutIf = new()
                            {
                                InSameTerritory = true,
                            }
                        }
                    }
                ]
            });
        }

        QuestSequence? endSequence = questRoot.QuestSequence.FirstOrDefault(x => x.Sequence == 255);
        if (endSequence == null)
        {
            questRoot.QuestSequence.Add(new QuestSequence
            {
                Sequence = 255,
                Steps =
                [
                    new QuestStep
                    {
                        DataId = leveStepData.GetTurnInDataId(leveInfo),
                        Position = leveStepData.GetTurnInPosition(leveInfo),
                        TerritoryId = _aetheryteData.TerritoryIds[leveStepData.AetheryteLocation],
                        InteractionType = EInteractionType.CompleteLeve,
                        AetheryteShortcut = leveStepData.AetheryteLocation,
                        AethernetShortcut = leveStepData.AethernetShortcut,
                        SkipConditions = new()
                        {
                            AetheryteShortcutIf = new()
                            {
                                InSameTerritory = true,
                            }
                        }
                    }
                ]
            });
        }
    }

    private sealed class LeveStepData
    {
        private readonly uint? _turnInDataId;
        private readonly Vector3? _turnInPosition;
        private readonly uint? _gathererTurnInDataId;
        private readonly Vector3? _gathererTurnInPosition;
        private readonly uint? _crafterTurnInDataId;
        private readonly Vector3? _crafterTurnInPosition;

        public LeveStepData(EAetheryteLocation aetheryteLocation,
            AethernetShortcut? aethernetShortcut,
            uint issuerDataId,
            Vector3 issuerPosition,
            uint? turnInDataId = null,
            Vector3? turnInPosition = null,
            uint? gathererTurnInDataId = null,
            Vector3? gathererTurnInPosition = null,
            uint? crafterTurnInDataId = null,
            Vector3? crafterTurnInPosition = null)
        {
            _turnInDataId = turnInDataId;
            _turnInPosition = turnInPosition;
            _gathererTurnInDataId = gathererTurnInDataId;
            _gathererTurnInPosition = gathererTurnInPosition;
            _crafterTurnInDataId = crafterTurnInDataId;
            _crafterTurnInPosition = crafterTurnInPosition;
            AetheryteLocation = aetheryteLocation;
            AethernetShortcut = aethernetShortcut;
            IssuerDataId = issuerDataId;
            IssuerPosition = issuerPosition;
        }

        public EAetheryteLocation AetheryteLocation { get; }
        public AethernetShortcut? AethernetShortcut { get; }
        public uint IssuerDataId { get; }
        public Vector3 IssuerPosition { get; }

        public uint GetTurnInDataId(LeveInfo leveInfo)
        {
            if (leveInfo.ClassJobs.Any(x => x.IsGatherer()))
                return _gathererTurnInDataId ?? _turnInDataId ?? IssuerDataId;
            else if (leveInfo.ClassJobs.Any(x => x.IsCrafter()))
                return _crafterTurnInDataId ?? _turnInDataId ?? IssuerDataId;
            else
                return _turnInDataId ?? IssuerDataId;
        }

        public Vector3 GetTurnInPosition(LeveInfo leveInfo)
        {
            if (leveInfo.ClassJobs.Any(x => x.IsGatherer()))
                return _gathererTurnInPosition ?? _turnInPosition ?? IssuerPosition;
            else if (leveInfo.ClassJobs.Any(x => x.IsCrafter()))
                return _crafterTurnInPosition ?? _turnInPosition ?? IssuerPosition;
            else
                return _turnInPosition ?? IssuerPosition;
        }
    }
}
