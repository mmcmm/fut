using System.Collections.Generic;

namespace EvFutBot.Utilities
{
    public class Demand
    {
        public List<Order> Ps4 { get; set; }
        public List<Order> Ps3 { get; set; }
        public List<Order> XboxOne { get; set; }
        public List<Order> Xbox360 { get; set; }
    }

    public class Order
    {
        public uint Amt { get; set; }
        public uint Id { get; set; }
        public uint Age { get; set; }
    }

    public class OrderCard
    {
        public long TradeId { get; set; }
        public uint BaseId { get; set; }
        public uint Bin { get; set; }
        public uint Value { get; set; }
        public uint StartPrice { get; set; }
    }
}