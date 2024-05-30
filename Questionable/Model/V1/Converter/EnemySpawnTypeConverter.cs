using System.Collections.Generic;

namespace Questionable.Model.V1.Converter;

public sealed class EnemySpawnTypeConverter() : EnumConverter<EEnemySpawnType>(Values)
{
    private static readonly Dictionary<EEnemySpawnType, string> Values = new()
    {
        { EEnemySpawnType.AfterInteraction, "AfterInteraction" },
        { EEnemySpawnType.AfterItemUse, "AfterItemUse" },
        { EEnemySpawnType.AutoOnEnterArea, "AutoOnEnterArea" },
    };
}
