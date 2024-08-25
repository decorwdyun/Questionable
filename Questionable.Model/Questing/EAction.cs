using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(ActionConverter))]
public enum EAction
{
    HeavySwing = 31,
    HeavyShot = 97,
    Cure = 120,
    Esuna = 7568,
    Physick = 190,
    BuffetSanuwa = 4931,
    BuffetGriffin = 4583,
    Fumigate = 5872,
    SiphonSnout = 18187,
    Cannonfire = 20121,
    RedGulal = 29382,
    YellowGulal = 29383,
    BlueGulal = 29384,
    ElectrixFlux = 29718,
    HopStep = 31116,

    CollectMiner = 240,
    ScourMiner = 22182,
    MeticulousMiner = 22184,
    ScrutinyMiner = 22185,

    CollectBotanist = 815,
    ScourBotanist = 22186,
    MeticulousBotanist = 22188,
    ScrutinyBotanist = 22189,

    SharpVision1 = 235,
    SharpVision2 = 237,
    SharpVision3 = 295,
    FieldMastery1 = 218,
    FieldMastery2 = 220,
    FieldMastery3 = 294,
}

public static class EActionExtensions
{
    public static bool RequiresMount(this EAction action)
    {
        return action
            is EAction.BuffetSanuwa
            or EAction.BuffetGriffin
            or EAction.Fumigate
            or EAction.SiphonSnout
            or EAction.Cannonfire
            or EAction.RedGulal
            or EAction.YellowGulal
            or EAction.BlueGulal
            or EAction.ElectrixFlux
            or EAction.HopStep;
    }
}
