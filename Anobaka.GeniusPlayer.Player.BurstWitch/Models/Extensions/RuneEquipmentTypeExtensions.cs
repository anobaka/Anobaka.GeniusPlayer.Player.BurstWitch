using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions
{
    public static class RuneEquipmentTypeExtensions
    {
        public static RuneStatType GetStatType(this RuneEquipmentType t)
        {
            switch (t)
            {
                case RuneEquipmentType.攻击:
                    return RuneStatType.攻击;
                case RuneEquipmentType.血量:
                    return RuneStatType.生命;
                case RuneEquipmentType.防御:
                    return RuneStatType.防御;
                default:
                    return 0;
            }
        }
    }

}
