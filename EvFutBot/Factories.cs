using System;
using System.Collections.Generic;
using System.Data;
using EvFutBot.Models;
using EvFutBot.Utilities;
using UltimateTeam.Toolkit.Constants;

namespace EvFutBot
{
    public class Factories
    {
        public static List<Account> MakeAccounts(IDataReader reader, AppVersion login)
        {
            List<Account> accounts = new List<Account>();
            while (reader.Read())
            {
                accounts.Add(new Account(
                    Convert.ToUInt32(reader["Id"]),
                    (string) reader["email"],
                    (string) reader["password"],
                    (string) reader["ut_security_answer"],
                    (string) reader["platform"],
                    (string) reader["gmail"],
                    (string) reader["gpassword"],
                    (string) reader["status"],
                    login
                    ));
            }
            return accounts;
        }

        public static Settings MakeSettings(IDataReader reader)
        {
            while (reader.Read())
            {
                string[] runforHours = ((string) reader["runfor_hours"]).Split(',');
                string[] rpmDelay = ((string) reader["rpm_delay"]).Split(',');
                try
                {
                    return new Settings(
                        new[] {Convert.ToByte(runforHours[0]), Convert.ToByte(runforHours[1])},
                        new[] {Convert.ToByte(rpmDelay[0]), Convert.ToByte(rpmDelay[1])},
                        (byte) reader["buy_percent"],
                        (byte) reader["sell_percent"],
                        (byte) reader["max_accounts"],
                        (byte) reader["batch"],
                        Convert.ToUInt32(reader["max_credits"]),
                        Convert.ToByte(reader["security_delay"]),
                        Convert.ToByte(reader["max_card_cost"]),
                        Convert.ToByte(reader["lowest_bin_nr"])
                        );
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message);
                }
            }
            return null;
        }

        public static List<OrderCard> MakeOrderCards(IDataReader reader)
        {
            List<OrderCard> cards = new List<OrderCard>();
            while (reader.Read())
            {
                cards.Add(new OrderCard
                {
                    TradeId = Convert.ToInt64(reader["trade_id"]),
                    BaseId = Convert.ToUInt32(reader["asset_id"]),
                    Bin = Convert.ToUInt32(reader["bin"]),
                    Value = Convert.ToUInt32(reader["value"]),
                    StartPrice = Convert.ToUInt32(reader["start_price"])
                });
            }
            return cards;
        }

        public static List<Player> MakePlayers(IDataReader reader)
        {
            List<Player> players = new List<Player>();

            while (reader.Read())
            {
                players.Add(new Player(
                    Convert.ToUInt32(reader["asset_id"]),
                    Convert.ToUInt32(reader["base_id"]),
                    (string) reader["common_name"],
                    (string) reader["first_name"],
                    (string) reader["last_name"],
                    Convert.ToByte(reader["rating"]),
                    Convert.ToUInt32(reader["std_pricePs3"]),
                    Convert.ToUInt32(reader["std_pricePs4"]),
                    Convert.ToUInt32(reader["std_priceXboxOne"]),
                    Convert.ToUInt32(reader["std_priceXbox360"]),
                    Convert.ToString(reader["added_on"]),
                    Convert.ToString(reader["platform"])
                    ));
            }
            return players;
        }

        public static List<Player> MakeCardWeightPlayers(IDataReader reader)
        {
            List<Player> players = new List<Player>();

            while (reader.Read())
            {
                players.Add(new Player(
                    Convert.ToUInt32(reader["asset_id"]),
                    Convert.ToUInt32(reader["base_id"]),
                    (string) reader["common_name"],
                    (string) reader["first_name"],
                    (string) reader["last_name"],
                    Convert.ToByte(reader["rating"]),
                    0,
                    0,
                    0,
                    0,
                    Convert.ToString(reader["updated"]),
                    "All"
                    ));
            }
            return players;
        }
    }
}