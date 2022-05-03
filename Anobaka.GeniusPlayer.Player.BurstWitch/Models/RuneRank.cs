using Anobaka.GeniusPlayer.Player.BurstWitch.Models.Constants;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Models
{
    public class RuneRank
    {
        public readonly RuneType Type;
        public readonly int Position;

        private readonly List<Rune> _runes = new();

        public RuneRank(RuneType type, int position)
        {
            Type = type;
            Position = position;
        }

        public void Add(Rune rune)
        {
            if (rune.Type != Type || rune.Position != Position)
            {
                throw new Exception($"Can not add {rune.Type}:{rune.Position} rune to {Type}:{Position} rune rank");
            }

            _runes.Add(rune);
        }

        public int CheckRank(Rune rune)
        {
            if (rune.Type != Type || rune.Position != Position)
            {
                throw new Exception($"Can not add {rune.Type}:{rune.Position} rune to {Type}:{Position} rune rank");
            }

            var s = _runes.ToList();
            s.Add(rune);
            return s.OrderByDescending(a => a.Score).ToList().IndexOf(rune);
        }

        public Rune GetByRank(int rank)
        {
            return _runes.Count > rank ? Runes[rank] : null;
        }

        public Rune[] Runes => _runes.OrderByDescending(t => t.Score).ToArray();
    }
}