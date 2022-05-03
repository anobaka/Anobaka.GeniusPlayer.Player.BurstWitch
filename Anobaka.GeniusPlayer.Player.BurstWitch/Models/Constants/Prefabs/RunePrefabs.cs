using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions;
using Bootstrap.Extensions;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants.Prefabs
{
    public class RunePrefabs
    {
        static RunePrefabs()
        {
            RemainCounts = new Dictionary<RuneEquipmentType, int>
                {
                    {RuneEquipmentType.攻击, 20},
                    {RuneEquipmentType.血量, 5},
                    {RuneEquipmentType.防御, 5}
                }.SelectMany(t =>
                    (SpecificEnumUtils<RuneType>.Values.Where(a => a.ToEquipmentType() == t.Key)
                        .Select(b => (Type: b, Count: t.Value))))
                .ToDictionary(a => a.Type, a => a.Count);
            foreach (var runeType in SpecificEnumUtils<RuneType>.Values.Where(s => !RemainCounts.ContainsKey(s)))
            {
                RemainCounts[runeType] = 0;
            }

            var bestStatCombinations = new Dictionary<int[], (bool IsMainStat, bool IsPercentage)[]>
            {
                {
                    new[] {1, 2, 4, 5}, new (bool IsMainStat, bool IsPercentage)[]
                    {
                        (false, false),
                        (false, true)
                    }
                },

                {
                    new[] {3}, new (bool IsMainStat, bool IsPercentage)[]
                    {
                        (true, false),
                        (false, false),
                        (false, true)
                    }
                },

                {
                    new[] {6}, new (bool IsMainStat, bool IsPercentage)[]
                    {
                        (true, true),
                        (false, false),
                        (false, true),
                    }
                }
            };

            MinimalScores = new Dictionary<RuneEquipmentType, Dictionary<int, decimal>>();
            foreach (var et in SpecificEnumUtils<RuneEquipmentType>.Values)
            {
                if (!MinimalScores.TryGetValue(et, out var positionAndScores))
                {
                    MinimalScores[et] = positionAndScores = new();
                }

                var st = et switch
                {
                    RuneEquipmentType.攻击 => RuneStatType.攻击,
                    RuneEquipmentType.血量 => RuneStatType.生命,
                    RuneEquipmentType.防御 => RuneStatType.防御,
                    _ => throw new ArgumentOutOfRangeException()
                };

                for (var i = 1; i <= 6; i++)
                {
                    var combination = bestStatCombinations.FirstOrDefault(t => t.Key.Contains(i)).Value;
                    positionAndScores[i] = combination.Sum(c =>
                    {
                        var v = RuneStatPrefabs.Get(st, c.IsMainStat, c.IsPercentage, RuneStatPrefabs.ValueType.Final);
                        return c.IsPercentage ? v * RuneStat.PercentageScoreRates[st] : v;
                    });
                }
            }
        }

        public const decimal MinimalScoreRate = 0.25m;

        public static readonly Dictionary<RuneType, int> RemainCounts;

        public static readonly Dictionary<RuneEquipmentType, Dictionary<int, decimal>> MinimalScores;
    }
}