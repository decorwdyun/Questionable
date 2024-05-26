using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public class AetheryteConverter : JsonConverter<EAetheryteLocation>
{
    private static readonly Dictionary<EAetheryteLocation, string> EnumToString = new()
    {
        { EAetheryteLocation.Limsa, "Limsa Lominsa" },
        { EAetheryteLocation.Gridania, "Gridania" },
        { EAetheryteLocation.Uldah, "Ul'dah" },
        { EAetheryteLocation.Ishgard, "Ishgard" },

        { EAetheryteLocation.RhalgrsReach, "Rhalgr's Reach" },
        { EAetheryteLocation.FringesCastrumOriens, "Fringes - Castrum Oriens" },
        { EAetheryteLocation.FringesPeeringStones, "Fringes - Peering Stones" },
        { EAetheryteLocation.PeaksAlaGannha, "Peaks - Ala Gannha" },
        { EAetheryteLocation.PeaksAlaGhiri, "Peaks - Ala Ghiri" },
        { EAetheryteLocation.LochsPortaPraetoria, "Lochs - Porta Praetoria" },
        { EAetheryteLocation.LochsAlaMhiganQuarter, "Lochs - Ala Mhigan Quarter" },
        { EAetheryteLocation.Kugane, "Kugane" },
        { EAetheryteLocation.RubySeaTamamizu, "Ruby Sea - Tamamizu" },
        { EAetheryteLocation.RubySeaOnokoro, "Ruby Sea - Onokoro" },
        { EAetheryteLocation.YanxiaNamai, "Yanxia - Namai" },
        { EAetheryteLocation.YanxiaHouseOfTheFierce, "Yanxia - House of the Fierce" },
        { EAetheryteLocation.AzimSteppeReunion, "Azim Steppe - Reunion" },
        { EAetheryteLocation.AzimSteppeDawnThrone, "Azim Steppe - Dawn Throne" },
        { EAetheryteLocation.AzimSteppeDhoroIloh, "Azim Steppe - Dhoro Iloh" },
        { EAetheryteLocation.DomanEnclave, "Doman Enclave" },
        { EAetheryteLocation.DomamEnclaveNorthern, "Doman Enclave - Northern Enclave" },
        { EAetheryteLocation.DomamEnclaveSouthern, "Doman Enclave - Southern Enclave" },

        { EAetheryteLocation.Crystarium, "Crystarium" },
        { EAetheryteLocation.Eulmore, "Eulmore" },
        { EAetheryteLocation.LakelandFortJobb, "Lakeland - Fort Jobb" },
        { EAetheryteLocation.LakelandOstallImperative, "Lakeland - Ostall Imperative" },
        { EAetheryteLocation.KholusiaStilltide, "Kholusia - Stilltide" },
        { EAetheryteLocation.KholusiaWright, "Kholusia - Wright" },
        { EAetheryteLocation.KholusiaTomra, "Kholusia - Tomra" },
        { EAetheryteLocation.AmhAraengMordSouq, "Amh Araeng - Mord Souq" },
        { EAetheryteLocation.AmhAraengInnAtJourneysHead, "Amh Araeng - Inn at Journey's Head" },
        { EAetheryteLocation.AmhAraengTwine, "Amh Araeng - Twine" },
        { EAetheryteLocation.RaktikaSlitherbough, "Rak'tika - Slitherbough" },
        { EAetheryteLocation.RaktikaFanow, "Rak'tika - Fanow" },
        { EAetheryteLocation.IlMhegLydhaLran, "Il Mheg - Lydha Lran" },
        { EAetheryteLocation.IlMhegPiaEnni, "Il Mheg - Pia Enni" },
        { EAetheryteLocation.IlMhegWolekdorf, "Il Mheg - Wolekdorf" },
        { EAetheryteLocation.TempestOndoCups, "Tempest - Ondo Cups" },
        { EAetheryteLocation.TempestMacarensesAngle, "Tempest - Macarenses Angle" },

        { EAetheryteLocation.OldSharlayan, "Old Sharlayan" },
        { EAetheryteLocation.RadzAtHan, "Radz-at-Han" },
        { EAetheryteLocation.LabyrinthosArcheion, "Labyrinthos - Archeion" },
        { EAetheryteLocation.LabyrinthosSharlayanHamlet, "Labyrinthos - Sharlayan Hamlet" },
        { EAetheryteLocation.LabyrinthosAporia, "Labyrinthos - Aporia" },
        { EAetheryteLocation.ThavnairYedlihmad, "Thavnair - Yedlihmad" },
        { EAetheryteLocation.ThavnairGreatWork, "Thavnair - Great Work" },
        { EAetheryteLocation.ThavnairPalakasStand, "Thavnair - Palaka's Stand" },
        { EAetheryteLocation.GarlemaldCampBrokenGlass, "Garlemald - Camp Broken Glass" },
        { EAetheryteLocation.GarlemaldTertium, "Garlemald - Tertium" },
        { EAetheryteLocation.MareLamentorumSinusLacrimarum, "Mare Lamentorum - Sinus Lacrimarum" },
        { EAetheryteLocation.MareLamentorumBestwaysBurrow, "Mare Lamentorum - Bestways Burrow" },
        { EAetheryteLocation.ElpisAnagnorisis, "Elpis - Anagnorisis" },
        { EAetheryteLocation.ElpisTwelveWonders, "Elpis - Twelve Wonders" },
        { EAetheryteLocation.ElpisPoietenOikos, "Elpis - Poieten Oikos" },
        { EAetheryteLocation.UltimaThuleReahTahra, "Ultima Thule - Reah Tahra" },
        { EAetheryteLocation.UltimaThuleAbodeOfTheEa, "Ultima Thula - Abode of the Ea" },
        { EAetheryteLocation.UltimaThuleBaseOmicron, "Ultima Thule - Base Omicron" }
    };

    private static readonly Dictionary<string, EAetheryteLocation> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override EAetheryteLocation Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string? str = reader.GetString();
        if (str == null)
            throw new JsonException();

        return StringToEnum.TryGetValue(str, out EAetheryteLocation value) ? value : throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, EAetheryteLocation value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(EnumToString[value]);
    }
}
