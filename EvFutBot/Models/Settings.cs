using System;

namespace EvFutBot.Models
{
    public class Settings
    {
        public Settings(byte[] runforHours, byte[] rpmDelay, byte buyPercent, byte sellPercent,
            byte maxAccounts, byte batch, uint maxCredits, byte securityDelay, byte maxCardCost,
            byte lowestBinNr)
        {
            BinPercent = buyPercent;
            BidPercent = buyPercent;
            SellPercent = sellPercent;
            MaxAccounts = maxAccounts;
            Batch = batch;
            MaxCredits = maxCredits;
            MaxCardCost = maxCardCost;
            LowestBinNr = lowestBinNr;
            SecurityDelay = securityDelay*1000;

            if (rpmDelay[0] < 4)
                throw new ArgumentOutOfRangeException(nameof(rpmDelay), "RPM Delay to small");

            var rand = new Random();
            RmpDelay = rand.Next(rpmDelay[0]*1000, rpmDelay[1]*1000);
            RmpDelayLow = rpmDelay[0]*1000;
            PreBidDelay = 0; // no delay
            RunforHours = rand.Next(runforHours[0], runforHours[1]);
            RmpDelayPrices = Convert.ToInt32(60/((decimal) 4900/RunforHours/60)*1000); // 5000 request limit per day
        }

        public int RunforHours { get; }
        public byte BinPercent { get; private set; }
        public byte BidPercent { get; private set; }
        public byte SellPercent { get; private set; }
        public byte MaxAccounts { get; private set; }
        public byte Batch { get; private set; }
        public uint MaxCredits { get; private set; }
        public byte MaxCardCost { get; set; }
        public byte LowestBinNr { get; set; }
        public int SecurityDelay { get; private set; }
        public int RmpDelay { get; }
        public int RmpDelayLow { get; private set; }
        public int RmpDelayPrices { get; private set; }
        public int PreBidDelay { get; private set; }
    }
}