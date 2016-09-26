using System.Collections.Generic;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public enum CardStatuses
        {
            New,
            Error,
            Bought,
            Locked
        }

        public enum Statuses
        {
            Coins,
            Inactive,
            Banned,
            Flagged,
            Error,
            Prices, // use for updating prices
            List // use for making list
        }

        private static readonly List<long> BuggedCardsWl = new List<long>();
    }
}