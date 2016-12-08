using System;

namespace EvFutBot.Models
{
    public class Settings
    {
        private readonly byte _batch;
        private readonly Random _rand;
        private readonly int _rmpDelayPrices;
        private readonly byte[] _rpmDelayRange;

        public Settings(byte[] runforHours, byte[] rpmDelay, byte buyPercent, byte sellPercent,
            byte maxAccounts, byte batch, uint maxCredits, byte securityDelay, byte maxCardCost,
            byte lowestBinNr)
        {
            _rand = new Random();

            BinPercent = buyPercent;
            BidPercent = buyPercent;
            SellPercent = sellPercent;
            MaxAccounts = maxAccounts;
            _batch = batch;
            MaxCredits = maxCredits;
            MaxCardCost = maxCardCost;
            LowestBinNr = lowestBinNr;
            SecurityDelay = securityDelay*1000;

            if (rpmDelay[0] < 4)
                throw new ArgumentOutOfRangeException(nameof(rpmDelay), "RPM Delay to small");

            _rpmDelayRange = rpmDelay;
            PreBidDelay = 0; // 2 seconds
            RunforHours = _rand.Next(runforHours[0], runforHours[1]);
            _rmpDelayPrices = Convert.ToInt32(60/((decimal) 4900/RunforHours/60)*1000); // 5000 request limit per day
        }

        public int RunforHours { get; }
        public byte BinPercent { get; private set; }
        public byte BidPercent { get; private set; }
        public byte SellPercent { get; private set; }
        public byte MaxAccounts { get; private set; }
        public int Batch => _rand.Next(_batch/2, _batch + _batch/2 + 1);
        public uint MaxCredits { get; private set; }
        public byte MaxCardCost { get; set; }
        public byte LowestBinNr { get; set; }
        public int SecurityDelay { get; private set; }

        public int RmpDelay => _rand.Next(_rpmDelayRange[0]*1000, _rpmDelayRange[1]*1000);
        public int RmpDelayLow => _rand.Next(3*1000, 9*1000);
        public int RmpDelayPrices => _rand.Next(_rmpDelayPrices/2, _rmpDelayPrices + _rmpDelayPrices/2 + 1);
        public int PreBidDelay { get; private set; }
    }
}