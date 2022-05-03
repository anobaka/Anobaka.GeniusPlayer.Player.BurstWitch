using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models
{
    public class RuneStat
    {
        public RuneStatType Type { get; set; }
        public decimal Value { get; set; }
        public int Position { get; set; }
        public bool IsPercentage => Value < 1;

        public static Dictionary<RuneStatType, decimal> ValueScoreRates = new()
        {
            { RuneStatType.攻击, 1 },
            { RuneStatType.防御, 1 },
            { RuneStatType.生命, 1 },
            { RuneStatType.爆伤, 0 },
            { RuneStatType.暴击, 0 },
        };

        public static Dictionary<RuneStatType, decimal> PercentageScoreRates = new()
        {
            { RuneStatType.攻击, 15000 },
            { RuneStatType.防御, 18000 },
            { RuneStatType.生命, 150000 },
            { RuneStatType.爆伤, 0 },
            { RuneStatType.暴击, 0 },
        };

        public bool IsFixedAttack => Type == RuneStatType.攻击 && Value > 1;
        public bool IsPercentageAttack => Type == RuneStatType.攻击 && Value < 1;
        public bool IsFixedHp => Type == RuneStatType.生命 && Value > 1;
        public bool IsPercentageHp => Type == RuneStatType.生命 && Value < 1;
        public bool IsFixedDefense => Type == RuneStatType.防御 && Value > 1;
        public bool IsPercentageDefense => Type == RuneStatType.防御 && Value < 1;


        // public static readonly RuneStat BestFixedAttackStat = new RuneStat
        // {
        //     Type = RuneStatType.攻击, Value = 633
        // };
        //
        // public static readonly RuneStat BestPercentageAttackStat = new RuneStat
        // {
        //     Type = RuneStatType.攻击, Value = 0.025m
        // };
        //
        // public static readonly RuneStat BestFixedHpStat = new RuneStat
        // {
        //     Type = RuneStatType.血量, Value = 3165
        // };
        //
        // public static readonly RuneStat BestPercentageHpStat = new RuneStat
        // {
        //     Type = RuneStatType.血量, Value = 0.025m
        // };
        // public static readonly RuneStat BestFixedDefenseStat = new RuneStat
        // {
        //     Type = RuneStatType.防御, Value = 633
        // };
        //
        // public static readonly RuneStat BestPercentageDefenseStat = new RuneStat
        // {
        //     Type = RuneStatType.防御, Value = 0.025m
        // };

        public int Score
        {
            get
            {
                var rates = Value < 1 ? PercentageScoreRates : ValueScoreRates;
                var rate = rates[Type];
                return (int)(rate * Value);
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is RuneStat rs && rs.Type == Type && rs.Value == Value;
        }

        public override int GetHashCode()
        {
            return $"{Type}-{Value}".GetHashCode();
        }
    }
}
