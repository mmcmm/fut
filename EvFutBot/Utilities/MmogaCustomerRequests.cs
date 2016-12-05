namespace EvFutBot.Utilities
{
    public class MmogaTrade
    {
        public uint Code { get; set; }
        public string TransactionId { get; set; }
        public string Platform { get; set; }
        public long TradeId { get; set; }
        public uint AssetId { get; set; }
        public uint MinPrice { get; set; }
        public uint CoinAmount { get; set; }
        public float OutpaymentValue { get; set; }
        public string OutpaymentCurrency { get; set; }
        public ulong LockExpires { get; set; }
    }

    public class MmogaTradeRequest
    {
        public string user;
        public string platform;
        public uint coinAmount;
        public string paymentGateway;
        public string paymentEmail;
        public string paymentCurrency;
        public uint time;
        public string hash;
    }
}