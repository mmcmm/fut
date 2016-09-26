using System.Collections.Generic;

namespace EvFutBot.Utilities
{
    public class DbPlayer
    {
        public int Id { get; set; }
        public int R { get; set; }
        public int N { get; set; }
        public string F { get; set; }
        public string L { get; set; }
        public string C { get; set; }
    }

    public class LegendsPlayer
    {
        public int Id { get; set; }
        public int R { get; set; }
        public int N { get; set; }
        public string F { get; set; }
        public string L { get; set; }
        public string C { get; set; }
    }

    public class PlayersDatabaseRequest
    {
        public List<DbPlayer> Players { get; set; }
        public List<LegendsPlayer> LegendsPlayers { get; set; }
    }
}