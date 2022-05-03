using Bootstrap.Extensions;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants.Prefabs
{
    public class RuneStatPrefabs
    {
        static RuneStatPrefabs()
        {
            var data =
                new Dictionary<HashSet<RuneStatType>, (bool IsPercentage, decimal MainInitMax, decimal MainFinalMax,
                    decimal SecondaryInitMax, decimal SecondaryFinalMax)>()
                {
                    {
                        new HashSet<RuneStatType> {RuneStatType.生命},
                        (false, 2051, 5998, 3153, 13042)
                    },
                    {
                        new HashSet<RuneStatType> {RuneStatType.暴击, RuneStatType.爆伤},
                        (false, 132, 388, 213, 933)
                    },
                    {
                        new HashSet<RuneStatType> {RuneStatType.攻击, RuneStatType.防御},
                        (false, 411, 1233, 633, 3267)
                    },
                    {
                        SpecificEnumUtils<RuneStatType>.Values.ToHashSet(),
                        (true, 0.032m, 0.095m, 0.025m, 0.118m)
                    }
                };

            {
                var groups = data.SelectMany(d => d.Key.Select(b => $"{b}-{d.Value.IsPercentage}")).GroupBy(a => a)
                    .ToArray();
                if (groups.Any(b => b.Count() > 1))
                {
                    throw new Exception("Duplicate rune values");
                }

                if (groups.Count() != SpecificEnumUtils<RuneStatType>.Values.Count * 2)
                {
                    throw new Exception("Rune values missed");
                }
            }

            Data = SpecificEnumUtils<RuneStatType>.Values.ToDictionary(a => a, a =>
            {
                var vs = data.Where(t => t.Key.Contains(a)).ToArray();
                var mainPercentageValue = vs.FirstOrDefault(b => b.Value.IsPercentage).Value;
                var mainFixedValue = vs.FirstOrDefault(b => !b.Value.IsPercentage).Value;

                var secondaryPercentageValue = vs.FirstOrDefault(b => b.Value.IsPercentage).Value;
                var secondaryFixedValue = vs.FirstOrDefault(b => !b.Value.IsPercentage).Value;

                var d = new Dictionary<bool, Dictionary<bool, Dictionary<ValueType, decimal>>>()
                {
                    {
                        true, new()
                        {
                            {
                                true,
                                new Dictionary<ValueType, decimal>
                                {
                                    {ValueType.Init, mainPercentageValue.MainInitMax},
                                    {ValueType.Final, mainPercentageValue.MainFinalMax},
                                    {
                                        ValueType.EachLevel,
                                        (mainPercentageValue.MainFinalMax - mainPercentageValue.MainInitMax) /
                                        (Rune.MaxLevel - 1)
                                    },
                                }
                            },
                            {
                                false,
                                new Dictionary<ValueType, decimal>
                                {
                                    {ValueType.Init, mainFixedValue.MainInitMax},
                                    {ValueType.Final, mainFixedValue.MainFinalMax},
                                    {
                                        ValueType.EachLevel,
                                        (mainFixedValue.MainFinalMax - mainFixedValue.MainInitMax) /
                                        (Rune.MaxLevel - 1)
                                    },
                                }
                            },
                        }
                    },
                    {
                        false, new()
                        {
                            {
                                true, new Dictionary<ValueType, decimal>
                                {
                                    {ValueType.Init, secondaryPercentageValue.SecondaryInitMax},
                                    {ValueType.Final, secondaryPercentageValue.SecondaryFinalMax},
                                    {
                                        ValueType.EachLevel,
                                        (secondaryPercentageValue.SecondaryFinalMax -
                                         secondaryPercentageValue.SecondaryInitMax) /
                                        (Rune.MaxLevel / (decimal) Rune.UpgradeLevelInterval)
                                    },
                                }
                            },
                            {
                                false, new Dictionary<ValueType, decimal>
                                {
                                    {ValueType.Init, secondaryFixedValue.SecondaryInitMax},
                                    {ValueType.Final, secondaryFixedValue.SecondaryFinalMax},
                                    {
                                        ValueType.EachLevel,
                                        (secondaryFixedValue.SecondaryFinalMax - secondaryFixedValue.SecondaryInitMax) /
                                        (Rune.MaxLevel / (decimal) Rune.UpgradeLevelInterval)
                                    },
                                }
                            },
                        }
                    }
                };

                return d;
            });
        }

        /// <summary>
        /// Stat type - Main stat - Percentage - Init - Final - Upgrade
        /// </summary>
        private static readonly
            Dictionary<RuneStatType, Dictionary<bool, Dictionary<bool, Dictionary<ValueType, decimal>>>> Data;

        public enum ValueType
        {
            Init,
            Final,
            EachLevel
        }

        public static decimal Get(RuneStatType type, bool isMainStat, bool isPercentage, ValueType vt)
        {
            return Data[type][isMainStat][isPercentage][vt];
        }

        // public const decimal 最高主属性固定值生命初始 = 2051;
        // public const decimal 最高主属性固定值生命上限 = 5998;
        // public const decimal 最高主属性固定值生命每级提升 = (最高主属性固定值生命上限 - 最高主属性固定值生命初始) / (Rune.MaxLevel - 1);
        // public const decimal 最高主属性固定值攻击防御初始 = 411;
        // public const decimal 最高主属性固定值攻击防御上限 = 1233;
        // public const decimal 最高主属性固定值攻击防御每级提升 = (最高主属性固定值攻击防御上限 - 最高主属性固定值攻击防御初始) / (Rune.MaxLevel - 1);
        // public const decimal 最高主属性百分比初始 = 0.032m;
        // public const decimal 最高主属性百分比上限 = 0.095m;
        // public const decimal 最高主属性百分比每级提升 = (最高主属性百分比上限 - 最高主属性百分比初始) / (Rune.MaxLevel - 1);
        //
        //
        // public const decimal 最高副属性固定值生命初始 = 3153;
        // public const decimal 最高副属性固定值生命上限 = 13042;
        // public const decimal 最高副属性固定值生命每级提升 = (最高副属性固定值生命上限 - 最高副属性固定值生命初始) / (Rune.MaxLevel - 1);
        // public const decimal 最高副属性固定值攻击防御初始 = 633;
        // public const decimal 最高副属性固定值攻击防御上限 = 3267;
        // public const decimal 最高副属性固定值攻击防御每级提升 = (最高副属性固定值攻击防御上限 - 最高副属性固定值攻击防御初始) / (Rune.MaxLevel - 1);
        // public const decimal 最高副属性百分比初始 = 0.025m;
        // public const decimal 最高副属性百分比上限 = 0.118m;
        // public const decimal 最高副属性百分比每级提升 = (最高副属性百分比上限 - 最高副属性百分比初始) / (Rune.MaxLevel - 1);
    }
}