﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Data;

[SuppressMessage("Performance", "CA1822")]
internal sealed class AlliedSocietyData
{
    public ReadOnlyDictionary<ushort, AlliedSocietyMountConfiguration> Mounts { get; } =
        new Dictionary<ushort, AlliedSocietyMountConfiguration>
        {
            { 66, new([1016093], EAetheryteLocation.SeaOfCloudsOkZundu) },
            { 79, new([1017031], EAetheryteLocation.DravanianForelandsAnyxTrine) },
            { 88, new([1017470, 1017432], EAetheryteLocation.ChurningMistsZenith) },
            { 89, new([1017322], EAetheryteLocation.ChurningMistsZenith) },
            { 147, new([1024777], EAetheryteLocation.FringesPeeringStones) },
            { 369, new([1051798], EAetheryteLocation.KozamaukaDockPoga) },
            { 221, new([1032663], EAetheryteLocation.RaktikaFanow) },
        }.AsReadOnly();

    public EAlliedSociety GetCommonAlliedSocietyTurnIn(ElementId elementId)
    {
        if (elementId is QuestId questId)
        {
            return questId.Value switch
            {
                >= 2171 and <= 2200 => EAlliedSociety.VanuVanu,
                >= 2261 and <= 2280 => EAlliedSociety.Vath,
                >= 2290 and <= 2319 => EAlliedSociety.Moogles,
                >= 5199 and <= 5226 => EAlliedSociety.Pelupelu,
                >= 3806 and <= 3833 => EAlliedSociety.Qitari,
                _ => EAlliedSociety.None,
            };
        }

        return EAlliedSociety.None;
    }

    public void GetCommonAlliedSocietyNpcs(EAlliedSociety alliedSociety, out uint[] normalNpcs, out uint[] mountNpcs)
    {
        if (alliedSociety == EAlliedSociety.VanuVanu)
        {
            normalNpcs = [1016088, 1016091, 1016092];
            mountNpcs = [1016093];
        }
        else if (alliedSociety == EAlliedSociety.Vath)
        {
            normalNpcs = [];
            mountNpcs = [1017031];
        }
        else if (alliedSociety == EAlliedSociety.Moogles)
        {
            normalNpcs = [];
            mountNpcs = [1017322, 1017470, 1017471];
        }
        else if (alliedSociety == EAlliedSociety.Qitari)
        {
            normalNpcs = [];
            mountNpcs = [1032663];
        }
        else if (alliedSociety == EAlliedSociety.Pelupelu)
        {
            normalNpcs = [];
            mountNpcs = [1051798];
        }
        else
        {
            normalNpcs = [];
            mountNpcs = [];
        }
    }
}

public sealed record AlliedSocietyMountConfiguration(IReadOnlyList<uint> IssuerDataIds, EAetheryteLocation ClosestAetheryte);
