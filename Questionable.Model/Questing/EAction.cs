using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(ActionConverter))]
public enum EAction
{
    Cure = 120,
    Esuna = 7568,
    Physick = 190,
    Buffet = 4931,
    Fumigate = 5872,
    SiphonSnout = 18187,
    RedGulal = 29382,
    YellowGulal = 29383,
    BlueGulal = 29384,

    CollectMiner = 240,
    ScourMiner = 22182,
    MeticulousMiner = 22184,
    ScrutinyMiner = 22185,

    CollectBotanist = 815,
    ScourBotanist = 22186,
    MeticulousBotanist = 22188,
    ScrutinyBotanist = 22189,


}

public static class EActionExtensions
{
    public static bool RequiresMount(this EAction action)
    {
        return action
            is EAction.Buffet
            or EAction.Fumigate
            or EAction.SiphonSnout
            or EAction.RedGulal
            or EAction.YellowGulal
            or EAction.BlueGulal;
    }
}
