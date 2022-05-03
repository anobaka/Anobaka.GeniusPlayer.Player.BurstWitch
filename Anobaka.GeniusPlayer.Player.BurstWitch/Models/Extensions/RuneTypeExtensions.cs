using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions
{
    public static class RuneTypeExtensions
    {
        public static RuneEquipmentType ToEquipmentType(this RuneType rt)
        {
            switch (rt)
            {
                case RuneType.斗争红树:
                case RuneType.怒火红火:
                case RuneType.觉醒红方:
                case RuneType.希望红圆:
                case RuneType.无尽黄牙:
                    return RuneEquipmentType.攻击;
                case RuneType.月时蓝月:
                case RuneType.守护蓝星:
                    return RuneEquipmentType.防御;
                case RuneType.生命绿水:
                case RuneType.神火绿花:
                    return RuneEquipmentType.血量;
                case RuneType.粉碎黄拳:
                default:
                    return 0;
            }
        }
    }
}