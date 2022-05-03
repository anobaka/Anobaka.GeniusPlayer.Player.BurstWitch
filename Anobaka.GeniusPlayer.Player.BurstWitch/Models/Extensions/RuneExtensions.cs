using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants.Prefabs;
using Bootstrap.Extensions;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions
{
    public static class RuneExtensions
    {
        public static decimal GetPotentialScore(this Rune _)
        {
            var superRune = _.JsonCopy().SuperUpgrade();
            return superRune.Score;
        }

        public static Rune SuperUpgrade(this Rune _)
        {
            var r = _.JsonCopy();

            var stats = r.Stats;

            var mainStat = stats[0];
            var mainUpgradeValue = RuneStatPrefabs.Get(mainStat.Type, true, mainStat.IsPercentage,
                RuneStatPrefabs.ValueType.EachLevel);
            mainStat.Value += mainUpgradeValue * r.RestUpgradeTimes;

            var priority = new (bool IsPercentage, int _)[]
            {
                (false, 0),
                (true, 0),
            };

            var targetStatType = r.Type.ToEquipmentType().GetStatType();
            var restSecondaryUpgradeTimes = r.RestSecondaryStatUpgradeTimes;

            for (var i = stats.Count; i < Rune.MaxStatCount; i++)
            {
                for (var j = 0; j < priority.Length; j++)
                {
                    var p = priority[j].IsPercentage;
                    if (!stats.Any(s => s.Type == targetStatType && p == s.IsPercentage))
                    {
                        stats.Add(new RuneStat
                        {
                            Position = stats.Count,
                            Type = targetStatType,
                            Value = RuneStatPrefabs.Get(targetStatType, false, p, RuneStatPrefabs.ValueType.Init)
                        });
                        break;
                    }
                }

                restSecondaryUpgradeTimes--;
            }

            if (restSecondaryUpgradeTimes > 0)
            {
                for (var i = 0; i < priority.Length; i++)
                {
                    var p = priority[i].IsPercentage;
                    var stat = stats.Skip(1).FirstOrDefault(a => a.Type == targetStatType && a.IsPercentage == p);
                    if (stat != null)
                    {
                        stat.Value += restSecondaryUpgradeTimes *
                                      RuneStatPrefabs.Get(targetStatType, false, p,
                                          RuneStatPrefabs.ValueType.EachLevel);
                        break;
                    }
                }
            }

            r.Level = Rune.MaxLevel;

            return r;
        }
    }
}