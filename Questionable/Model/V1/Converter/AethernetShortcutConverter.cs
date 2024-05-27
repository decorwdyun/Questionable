﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public sealed class AethernetShortcutConverter : JsonConverter<AethernetShortcut>
{
    private static readonly Dictionary<EAetheryteLocation, string> EnumToString = new()
    {
        { EAetheryteLocation.Limsa, "[Limsa Lominsa] Aetheryte Plaza" },
        { EAetheryteLocation.LimsaArcanist, "[Limsa Lominsa] Arcanist's Guild" },
        { EAetheryteLocation.LimsaFisher, "[Limsa Lominsa] Fishermen's Guild" },
        { EAetheryteLocation.LimsaHawkersAlley, "[Limsa Lominsa] Hawker's Alley" },
        { EAetheryteLocation.LimsaAftcastle, "[Limsa Lominsa] The Aftcastle" },
        { EAetheryteLocation.LimsaCulinarian, "[Limsa Lominsa] Culinarian's Guild" },
        { EAetheryteLocation.LimsaMarauder, "[Limsa Lominsa] Marauder's Guild" },
        { EAetheryteLocation.LimsaAirship, "[Limsa Lominsa] Airship Landing" },
        { EAetheryteLocation.Gridania, "[Gridania] Aetheryte Plaza" },
        { EAetheryteLocation.GridaniaArcher, "[Gridania] Archer's Guild" },
        { EAetheryteLocation.GridaniaLeatherworker, "[Gridania] Leatherworker's Guild & Shaded Bower" },
        { EAetheryteLocation.GridaniaLancer, "[Gridania] Lancer's Guild" },
        { EAetheryteLocation.GridaniaConjurer, "[Gridania] Conjurer's Guild" },
        { EAetheryteLocation.GridaniaBotanist, "[Gridania] Botanist's Guild" },
        { EAetheryteLocation.GridaniaAmphitheatre, "[Gridania] Mih Khetto's Amphitheatre" },
        { EAetheryteLocation.GridaniaAirship, "[Gridania] Airship Landing" },
        { EAetheryteLocation.Uldah, "[Ul'dah] Aetheryte Plaza" },
        { EAetheryteLocation.UldahAdventurers, "[Ul'dah] Adventurer's Guild" },
        { EAetheryteLocation.UldahThaumaturge, "[Ul'dah] Thaumaturge's Guild" },
        { EAetheryteLocation.UldahGladiator, "[Ul'dah] Gladiator's Guild" },
        { EAetheryteLocation.UldahMiner, "[Ul'dah] Miner's Guild" },
        { EAetheryteLocation.UldahWeaver, "[Ul'dah] Weavers' Guild" },
        { EAetheryteLocation.UldahGoldsmith, "[Ul'dah] Goldsmiths' Guild" },
        { EAetheryteLocation.UldahSapphireAvenue, "[Ul'dah] Sapphire Avenue Exchange" },
        { EAetheryteLocation.UldahAlchemist, "[Ul'dah] Alchemists' Guild" },
        { EAetheryteLocation.UldahChamberOfRule, "[Ul'dah] The Chamber of Rule" },
        { EAetheryteLocation.UldahAirship, "[Ul'dah] Airship Landing" },
        { EAetheryteLocation.Ishgard, "[Ishgard] Aetheryte Plaza" },
        { EAetheryteLocation.IshgardForgottenKnight, "[Ishgard] The Forgotten Knight" },
        { EAetheryteLocation.IshgardSkysteelManufactory, "[Ishgard] Skysteel Manufactory" },
        { EAetheryteLocation.IshgardBrume, "[Ishgard] The Brume" },
        { EAetheryteLocation.IshgardAthenaeumAstrologicum, "[Ishgard] Athenaeum Astrologicum" },
        { EAetheryteLocation.IshgardJeweledCrozier, "[Ishgard] The Jeweled Crozier" },
        { EAetheryteLocation.IshgardSaintReymanaudsCathedral, "[Ishgard] Saint Reymanaud's Cathedral" },
        { EAetheryteLocation.IshgardTribunal, "[Ishgard] The Tribunal" },
        { EAetheryteLocation.IshgardLastVigil, "[Ishgard] The Last Vigil" },
        { EAetheryteLocation.Idyllshire, "[Idyllshire] Aetheryte Plaza" },
        { EAetheryteLocation.IdyllshireWest, "[Idyllshire] West Idyllshire" },
        { EAetheryteLocation.RhalgrsReach, "[Rhalgr's Reach] Aetheryte Plaza" },
        { EAetheryteLocation.RhalgrsReachWest, "[Rhalgr's Reach] Western Rhalgr's Reach" },
        { EAetheryteLocation.RhalgrsReachNorthEast, "[Rhalgr's Reach] Northeastern Rhalgr's Reach" },
        { EAetheryteLocation.Kugane, "[Kugane] Aetheryte Plaza" },
        { EAetheryteLocation.KuganeShiokazeHostelry, "[Kugane] Shiokaze Hostelry" },
        { EAetheryteLocation.KuganePier1, "[Kugane] Pier #1" },
        { EAetheryteLocation.KuganeThavnairianConsulate, "[Kugane] Thavnairian Consulate" },
        { EAetheryteLocation.KuganeMarkets, "[Kugane] Kogane Dori Markets" },
        { EAetheryteLocation.KuganeBokairoInn, "[Kugane] Bokairo Inn" },
        { EAetheryteLocation.KuganeRubyBazaar, "[Kugane] The Ruby Bazaar" },
        { EAetheryteLocation.KuganeSekiseigumiBarracks, "[Kugane] Sekiseigumi Barracks" },
        { EAetheryteLocation.KuganeRakuzaDistrict, "[Kugane] Rakuza District" },
        { EAetheryteLocation.KuganeAirship, "[Kugane] Airship Landing" },
        { EAetheryteLocation.Crystarium, "[Crystarium] Aetheryte Plaza" },
        { EAetheryteLocation.CrystariumMarkets, "[Crystarium] Musica Universalis Markets" },
        { EAetheryteLocation.CrystariumThemenosRookery, "[Crystarium] Themenos Rookery" },
        { EAetheryteLocation.CrystariumDossalGate, "[Crystarium] The Dossal Gate" },
        { EAetheryteLocation.CrystariumPendants, "[Crystarium] The Pendants" },
        { EAetheryteLocation.CrystariumAmaroLaunch, "[Crystarium] The Amaro Launch" },
        { EAetheryteLocation.CrystariumCrystallineMean, "[Crystarium] The Crystalline Mean" },
        { EAetheryteLocation.CrystariumCabinetOfCuriosity, "[Crystarium] The Cabinet of Curiosity" },
        { EAetheryteLocation.Eulmore, "[Eulmore] Aetheryte Plaza" },
        { EAetheryteLocation.EulmoreSoutheastDerelict, "[Eulmore] Southeast Derelicts" },
        { EAetheryteLocation.EulmoreNightsoilPots, "[Eulmore] Nightsoil Pots" },
        { EAetheryteLocation.EulmoreGloryGate, "[Eulmore] The Glory Gate" },
        { EAetheryteLocation.EulmoreMainstay, "[Eulmore] The Mainstay" },
        { EAetheryteLocation.OldSharlayan, "[Old Sharlayan] Aetheryte Plaza" },
        { EAetheryteLocation.OldSharlayanStudium, "[Old Sharlayan] The Studium" },
        { EAetheryteLocation.OldSharlayanBaldesionAnnex, "[Old Sharlayan] The Baldesion Annex" },
        { EAetheryteLocation.OldSharlayanRostra, "[Old Sharlayan] The Rostra" },
        { EAetheryteLocation.OldSharlayanLeveilleurEstate, "[Old Sharlayan] The Leveilleur Estate" },
        { EAetheryteLocation.OldSharlayanJourneysEnd, "[Old Sharlayan] Journey's End" },
        { EAetheryteLocation.OldSharlayanScholarsHarbor, "[Old Sharlayan] Scholar's Harbor" },
        { EAetheryteLocation.RadzAtHan, "[Radz-at-Han] Aetheryte Plaza" },
        { EAetheryteLocation.RadzAtHanMeghaduta, "[Radz-at-Han] Meghaduta" },
        { EAetheryteLocation.RadzAtHanRuveydahFibers, "[Radz-at-Han] Ruveydah Fibers" },
        { EAetheryteLocation.RadzAtHanAirship, "[Radz-at-Han] Airship Landing" },
        { EAetheryteLocation.RadzAtHanAlzadaalsPeace, "[Radz-at-Han] Alzadaal's Peace" },
        { EAetheryteLocation.RadzAtHanHallOfTheRadiantHost, "[Radz-at-Han] Hall of the Radiant Host" },
        { EAetheryteLocation.RadzAtHanMehrydesMeyhane, "[Radz-at-Han] Mehryde's Meyhane" },
        { EAetheryteLocation.RadzAtHanKama, "[Radz-at-Han] Kama" },
        { EAetheryteLocation.RadzAtHanHighCrucible, "[Radz-at-Han] The High Crucible of Al-Kimiya" }
    };

    private static readonly Dictionary<string, EAetheryteLocation> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override AethernetShortcut Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string from = reader.GetString() ?? throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string to = reader.GetString() ?? throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException();

        return new AethernetShortcut
        {
            From = StringToEnum.TryGetValue(from, out var fromEnum) ? fromEnum : throw new JsonException(),
            To = StringToEnum.TryGetValue(to, out var toEnum) ? toEnum : throw new JsonException()
        };
    }

    public override void Write(Utf8JsonWriter writer, AethernetShortcut value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartArray();
        writer.WriteStringValue(EnumToString[value.From]);
        writer.WriteStringValue(EnumToString[value.To]);
        writer.WriteEndArray();
    }
}
