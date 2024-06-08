﻿using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

internal sealed class AetheryteConverter() : EnumConverter<EAetheryteLocation>(Values)
{
    private static readonly Dictionary<EAetheryteLocation, string> Values = new()
    {
        { EAetheryteLocation.Gridania, "Gridania" },

        { EAetheryteLocation.CentralShroudBentbranchMeadows, "Central Shroud - Bentbranch Meadows" },
        { EAetheryteLocation.EastShroudHawthorneHut, "East Shroud - Hawthorne Hut" },
        { EAetheryteLocation.SouthShroudQuarrymill, "South Shroud - Quarrymill" },
        { EAetheryteLocation.SouthShroudCampTranquil, "South Shroud - Camp Tranquil" },
        { EAetheryteLocation.NorthShroudFallgourdFloat, "North Shroud - Fallgourd Float" },

        { EAetheryteLocation.Uldah, "Ul'dah" },
        { EAetheryteLocation.WesternThanalanHorizon, "Western Thanalan - Horizon" },
        { EAetheryteLocation.CentralThanalanBlackBrushStation, "Central Thanalan - Black Brush Station" },
        { EAetheryteLocation.EasternThanalanCampDrybone, "Eastern Thanalan - Camp Drybone" },
        { EAetheryteLocation.SouthernThanalanLittleAlaMhigo, "Southern Thanalan - Little Ala Mhigo" },
        { EAetheryteLocation.SouthernThanalanForgottenSprings, "Southern Thanalan - Forgotten Springs" },
        { EAetheryteLocation.NorthernThanalanCampBluefog, "Northern Thanalan - Camp Bluefog" },
        { EAetheryteLocation.NorthernThanalanCeruleumProcessingPlant, "Northern Thanalan - Ceruleum Processing Plant" },

        { EAetheryteLocation.Limsa, "Limsa Lominsa" },
        { EAetheryteLocation.MiddleLaNosceaSummerfordFarms, "Middle La Noscea - Summerford Farms" },
        { EAetheryteLocation.LowerLaNosceaMorabyDrydocks, "Lower La Noscea - Moraby Drydocks" },
        { EAetheryteLocation.EasternLaNosceaCostaDelSol, "Eastern La Noscea - Costa Del Sol" },
        { EAetheryteLocation.EasternLaNosceaWineport, "Eastern La Noscea - Wineport" },
        { EAetheryteLocation.WesternLaNosceaSwiftperch, "Western La Noscea - Swiftperch" },
        { EAetheryteLocation.WesternLaNosceaAleport, "Western La Noscea - Aleport" },
        { EAetheryteLocation.UpperLaNosceaCampBronzeLake, "Upper La Noscea - Camp Bronze Lake" },
        { EAetheryteLocation.OuterLaNosceaCampOverlook, "Outer La Noscea - Camp Overlook" },

        { EAetheryteLocation.CoerthasCentralHighlandsCampDragonhead, "Coerthas Central Highlands - Camp Dragonhead" },
        { EAetheryteLocation.MorDhona, "Mor Dhona" },
        { EAetheryteLocation.GoldSaucer, "Gold Saucer" },
        { EAetheryteLocation.WolvesDenPier, "Wolves' Den Pier" },

        { EAetheryteLocation.Ishgard, "Ishgard" },
        { EAetheryteLocation.Idyllshire, "Idyllshire" },
        { EAetheryteLocation.CoerthasWesternHighlandsFalconsNest, "Coerthas Western Highlands - Falcon's Nest" },
        { EAetheryteLocation.SeaOfCloudsCampCloudtop, "The Sea of Clouds - Camp Cloudtop" },
        { EAetheryteLocation.SeaOfCloudsOkZundu, "The Sea of Clouds - Ok' Zundu" },
        { EAetheryteLocation.AzysLlaHelix, "Azys Lla - Helix" },
        { EAetheryteLocation.DravanianForelandsTailfeather, "The Dravanian Forelands - Tailfeather" },
        { EAetheryteLocation.DravanianForelandsAnyxTrine, "The Dravanian Forelands - Anyx Trine" },
        { EAetheryteLocation.ChurningMistsMoghome, "The Churning Mists - Moghome" },
        { EAetheryteLocation.ChurningMistsZenith, "The Churning Mists - Zenith" },

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
        { EAetheryteLocation.UltimaThuleAbodeOfTheEa, "Ultima Thule - Abode of the Ea" },
        { EAetheryteLocation.UltimaThuleBaseOmicron, "Ultima Thule - Base Omicron" }
    };

    public static bool IsLargeAetheryte(EAetheryteLocation aetheryte) => Values.ContainsKey(aetheryte);
}
