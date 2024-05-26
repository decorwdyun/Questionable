using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets2;
using Questionable.Model.V1;

namespace Questionable.Data;

internal sealed class AetheryteData
{
    public ReadOnlyDictionary<EAetheryteLocation, Vector3> Locations { get; } =
        new Dictionary<EAetheryteLocation, Vector3>
            {
                { EAetheryteLocation.Gridania, new(32.913696f, 2.670288f, 30.014404f) },
                { EAetheryteLocation.GridaniaArcher, new(166.58276f, -1.7243042f, 86.13721f) },
                { EAetheryteLocation.GridaniaLeatherworker, new(101.27405f, 9.018005f, -111.31464f) },
                { EAetheryteLocation.GridaniaLancer, new(121.23291f, 12.649658f, -229.63306f) },
                { EAetheryteLocation.GridaniaConjurer, new(-145.15906f, 4.9591064f, -11.7647705f) },
                { EAetheryteLocation.GridaniaBotanist, new(-311.0857f, 7.94989f, -177.05048f) },
                { EAetheryteLocation.GridaniaAmphitheatre, new(-73.92999f, 7.9804688f, -140.15417f) },

                { EAetheryteLocation.Uldah, new(-144.51825f, -1.3580933f, -169.6651f) },
                { EAetheryteLocation.UldahAdventurers, new(64.22522f, 4.5318604f, -115.31244f) },
                { EAetheryteLocation.UldahThaumaturge, new(-154.83331f, 14.633362f, 73.07532f) },
                { EAetheryteLocation.UldahGladiator, new(-53.849182f, 10.696533f, 12.222412f) },
                { EAetheryteLocation.UldahMiner, new(33.49353f, 13.229492f, 113.206665f) },
                { EAetheryteLocation.UldahAlchemist, new(-98.25293f, 42.34375f, 88.45642f) },
                { EAetheryteLocation.UldahWeaver, new(89.64673f, 12.924377f, 58.27417f) },
                { EAetheryteLocation.UldahGoldsmith, new(-19.333252f, 14.602844f, 72.03784f) },
                { EAetheryteLocation.UldahSapphireAvenue, new(131.9447f, 4.714966f, -29.800903f) },
                { EAetheryteLocation.UldahChamberOfRule, new(6.6376343f, 30.655273f, -24.826477f) },

                { EAetheryteLocation.Limsa, new(-84.031494f, 20.767456f, 0.015197754f) },
                { EAetheryteLocation.LimsaAftcastle, new(16.067688f, 40.787354f, 68.80286f) },
                { EAetheryteLocation.LimsaCulinarian, new(-56.50421f, 44.47998f, -131.45648f) },
                { EAetheryteLocation.LimsaArcanist, new(-335.1645f, 12.619202f, 56.381958f) },
                { EAetheryteLocation.LimsaFisher, new(-179.40033f, 4.8065186f, 182.97095f) },
                { EAetheryteLocation.LimsaMarauder, new(-5.1728516f, 44.63257f, -218.06671f) },
                { EAetheryteLocation.LimsaHawkersAlley, new(-213.61108f, 16.739136f, 51.80432f) },

                // ... missing a few

                { EAetheryteLocation.Crystarium, new(-65.0188f, 4.5318604f, 0.015197754f) },
                { EAetheryteLocation.CrystariumMarkets, new(-6.149414f, -7.736328f, 148.72961f) },
                { EAetheryteLocation.CrystariumThemenosRookery, new(-107.37775f, -0.015319824f, -58.762512f) },
                { EAetheryteLocation.CrystariumDossalGate, new(64.86609f, -0.015319824f, -18.173523f) },
                { EAetheryteLocation.CrystariumPendants, new(35.477173f, -0.015319824f, 222.58337f) },
                { EAetheryteLocation.CrystariumAmaroLaunch, new(66.60559f, 35.99597f, -131.09033f) },
                { EAetheryteLocation.CrystariumCrystallineMean, new(-52.506348f, 19.97406f, -173.35773f) },
                { EAetheryteLocation.CrystariumCabinetOfCuriosity, new(-54.398438f, -37.70508f, -241.07733f) },

                { EAetheryteLocation.Eulmore, new(0.015197754f, 81.986694f, 0.93078613f) },
                { EAetheryteLocation.EulmoreMainstay, new(10.940674f, 36.087524f, -4.196289f) },
                { EAetheryteLocation.EulmoreNightsoilPots, new(-54.093323f, -0.83929443f, 52.140015f) },
                { EAetheryteLocation.EulmoreGloryGate, new(6.9122925f, 6.240906f, -56.351562f) },
                { EAetheryteLocation.EulmoreSoutheastDerelict, new(71.82422f, -10.391418f, 65.32385f) },

                // ... missing a few

                { EAetheryteLocation.OldSharlayan, new(0.07623291f, 4.8065186f, -0.10687256f) },
                { EAetheryteLocation.OldSharlayanStudium, new(-291.1574f, 20.004517f, -74.143616f) },
                { EAetheryteLocation.OldSharlayanBaldesionAnnex, new(-92.21033f, 2.304016f, 29.709229f) },
                { EAetheryteLocation.OldSharlayanRostra, new(-36.94214f, 41.367188f, -156.6034f) },
                { EAetheryteLocation.OldSharlayanLeveilleurEstate, new(204.79126f, 21.774597f, -118.73047f) },
                { EAetheryteLocation.OldSharlayanJourneysEnd, new(206.22559f, 1.8463135f, 13.77887f) },
                { EAetheryteLocation.OldSharlayanScholarsHarbor, new(16.494995f, -16.250854f, 127.73328f) },

                { EAetheryteLocation.RadzAtHan, new(25.986084f, 3.250122f, -27.023743f) },
                { EAetheryteLocation.RadzAtHanMeghaduta, new(-365.95715f, 44.99878f, -31.815125f) },
                { EAetheryteLocation.RadzAtHanRuveydahFibers, new(-156.14563f, 35.99597f, 27.725586f) },
                { EAetheryteLocation.RadzAtHanAirship, new(-144.33508f, 27.969727f, 202.2583f) },
                { EAetheryteLocation.RadzAtHanAlzadaalsPeace, new(6.6071167f, -2.02948f, 110.55151f) },
                { EAetheryteLocation.RadzAtHanHallOfTheRadiantHost, new(-141.37488f, 3.982544f, -98.435974f) },
                { EAetheryteLocation.RadzAtHanMehrydesMeyhane, new(-42.61847f, -0.015319824f, -197.61963f) },
                { EAetheryteLocation.RadzAtHanKama, new(129.59485f, 26.993164f, 13.473633f) },
                { EAetheryteLocation.RadzAtHanHighCrucible, new(57.90796f, -24.704407f, -210.6203f) },

                { EAetheryteLocation.LabyrinthosArcheion, new(443.5338f, 170.6416f, -476.18835f) },
                { EAetheryteLocation.LabyrinthosSharlayanHamlet, new(8.377136f, -27.542603f, -46.67737f) },
                { EAetheryteLocation.LabyrinthosAporia, new(-729.18286f, -27.634155f, 302.1438f) },
                { EAetheryteLocation.ThavnairYedlihmad, new(193.49963f, 6.9733276f, 629.2362f) },
                { EAetheryteLocation.ThavnairGreatWork, new(-527.48914f, 4.776001f, 36.75891f) },
                { EAetheryteLocation.ThavnairPalakasStand, new(405.1422f, 5.2643433f, -244.4953f) },
                { EAetheryteLocation.GarlemaldCampBrokenGlass, new(-408.10254f, 24.15503f, 479.9724f) },
                { EAetheryteLocation.GarlemaldTertium, new(518.9136f, -35.324707f, -178.36273f) },
                { EAetheryteLocation.MareLamentorumSinusLacrimarum, new(-566.2471f, 134.66089f, 650.6294f) },
                { EAetheryteLocation.MareLamentorumBestwaysBurrow, new(-0.015319824f, -128.83197f, -512.0165f) },
                { EAetheryteLocation.ElpisAnagnorisis, new(159.96033f, 11.703674f, 126.878784f) },
                { EAetheryteLocation.ElpisTwelveWonders, new(-633.7225f, -19.821533f, 542.56494f) },
                { EAetheryteLocation.ElpisPoietenOikos, new(-529.9001f, 161.24207f, -222.2782f) },
                { EAetheryteLocation.UltimaThuleReahTahra, new(-544.152f, 74.32666f, 269.6421f) },
                { EAetheryteLocation.UltimaThuleAbodeOfTheEa, new(64.286255f, 272.48022f, -657.49603f) },
                { EAetheryteLocation.UltimaThuleBaseOmicron, new(489.2804f, 437.5829f, 333.63843f) },
            }
            .AsReadOnly();

    public ReadOnlyDictionary<EAetheryteLocation, string> AethernetNames { get; }
    public ReadOnlyDictionary<EAetheryteLocation, ushort> TerritoryIds { get; }

    public AetheryteData(IDataManager dataManager)
    {
        Dictionary<EAetheryteLocation, string> aethernetNames = new();
        Dictionary<EAetheryteLocation, ushort> territoryIds = new();
        foreach (var aetheryte in dataManager.GetExcelSheet<Aetheryte>()!.Where(x => x.RowId > 0))
        {
            string? aethernetName = aetheryte.AethernetName?.Value?.Name.ToString();
            if (!string.IsNullOrEmpty(aethernetName))
                aethernetNames[(EAetheryteLocation)aetheryte.RowId] = aethernetName;

            if (aetheryte.Territory != null && aetheryte.Territory.Row > 0)
                territoryIds[(EAetheryteLocation)aetheryte.RowId] = (ushort)aetheryte.Territory.Row;
        }

        AethernetNames = aethernetNames.AsReadOnly();
        TerritoryIds = territoryIds.AsReadOnly();
    }

    public float CalculateDistance(Vector3 fromPosition, ushort fromTerritoryType, EAetheryteLocation to)
    {
        if (!TerritoryIds.TryGetValue(to, out ushort toTerritoryType) || fromTerritoryType != toTerritoryType)
            return float.MaxValue;

        if (!Locations.TryGetValue(to, out Vector3 toPosition))
            return float.MaxValue;

        return (fromPosition - toPosition).Length();
    }
}
