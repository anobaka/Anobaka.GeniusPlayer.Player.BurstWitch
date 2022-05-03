using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;
using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Extensions;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models
{
    public class Rune
    {
        public const int MaxLevel = 15;
        public const int UpgradeLevelInterval = 3;
        public const int MaxStatCount = 5;
        public List<RuneStat> Stats = new();
        public RuneType Type { get; set; }
        public ItemQuality Quality { get; set; }

        public int Position { get; set; }

        private int _level;

        public int Level
        {
            get => _level;
            set
            {
                if (value >= 1 && value <= 15)
                {
                    _level = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Level can not be {value}");
                }
            }
        }

        public bool Locked { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is Rune r)
            {
                return r.Type == Type
                       && r.Position == Position
                       && r.Level == Level
                       && r.Quality == Quality
                       && r.Stats.Any()
                       && r.Stats.Count == Stats.Count
                       && r.Stats.All(Stats.Contains);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{Type}-{Quality}-{Position}-{Level}-{string.Join('-', Stats.Select(a => a.GetHashCode()))}"
                .GetHashCode();
        }

        public int Score
        {
            get { return Stats.Where(a => a.Type == Type.ToEquipmentType().GetStatType()).Sum(a => a.Score); }
        }

        public int RestSecondaryStatUpgradeTimes => MaxLevel / UpgradeLevelInterval - Level / 3;
        public int RestUpgradeTimes => MaxLevel - Level;

        public override string ToString()
        {
            var score = Score;
            var potentialScore = this.SuperUpgrade().Score;
            return
                $"{Type}-{Position}|{Quality}|{Level}|{Locked}|{string.Join(';', Stats.Select(t => $"{t.Type}:{(t.Value > 1 ? t.Value : $"{$"{t.Value * 100}".TrimEnd('0')}%")}"))}|{(score == potentialScore ? score : $"{score}->{potentialScore}")}";
        }
    }
}