using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using EvFutBot.Models;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot
{
    public static class Database
    {
        // pull these from enviroment
        private static readonly string DbHost = ConfigurationManager.AppSettings["dbHost"];

        private static readonly string DbName = ConfigurationManager.AppSettings["user"];
        private static readonly string DbPassword = ConfigurationManager.AppSettings["dbPassword"];
        private static readonly string DbUser = ConfigurationManager.AppSettings["user"];
        private static readonly string DbUserHost = ConfigurationManager.AppSettings["dbUserHost"];
        private static readonly string SshPassword = ConfigurationManager.AppSettings["sshPassword"];

        private static readonly string SshUser = ConfigurationManager.AppSettings["user"];

        public static SshTunnel Tunnel;
        public static MySqlConnectionStringBuilder Cs;

        static Database()
        {
            SshConnect();
        }

        public static List<Account> GetAccounts(byte max, AppVersion login)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "SELECT accounts.*, email_accounts.address as gmail, email_accounts.password as gpassword " +
                    "FROM accounts LEFT JOIN email_accounts ON accounts.email_account = email_accounts.Id WHERE " +
                    $"accounts.server = '{Environment.MachineName}' AND accounts.status != '{Account.Statuses.Inactive}' " +
                    $"ORDER BY Id ASC LIMIT 0, {max};";
                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakeAccounts(reader, login);
            }
        }

        public static Settings GetSettings()
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = $"SELECT * FROM settings WHERE server = '{Environment.MachineName}' " +
                                  "OR server = 'All' ORDER BY Id DESC LIMIT 0, 1;";
                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakeSettings(reader);
            }
        }

        public static List<OrderCard> GetCustomerCards(Platform platform, string email, uint credits)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "UPDATE customer_cards SET status=@status1, account=@account " +
                    "WHERE bin <= @credits AND status=@status2 AND platform=@platform AND account='' LIMIT 1;";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@credits", credits);
                cmd.Parameters.AddWithValue("@status1", Account.CardStatuses.Locked.ToString());
                cmd.Parameters.AddWithValue("@status2", Account.CardStatuses.New.ToString());
                cmd.Parameters.AddWithValue("@account", email);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();

                cmd.CommandText =
                    "SELECT * FROM customer_cards WHERE status=@status AND platform=@platform " +
                    "AND account=@account LIMIT 0, 1;";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@status", Account.CardStatuses.Locked.ToString());
                cmd.Parameters.AddWithValue("@account", email);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());

                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakeOrderCards(reader);
            }
        }

        public static List<Player> GetPlayers(byte max, uint maxPrice, Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();
                // we overwrite old gen
                Platform platformList = platform;
                switch (platformList)
                {
                    case Platform.Ps3:
                        platformList = Platform.Ps4;
                        break;
                    case Platform.Xbox360:
                        platformList = Platform.XboxOne;
                        break;
                }

                cmd.CommandText = $"SELECT asset_id FROM players WHERE std_price{platform}<={maxPrice} " +
                                  $"AND std_price{platform}>0 AND (platform='{platformList}' OR platform='All') LIMIT 0, 2000";

                IDataReader reader = cmd.ExecuteReader();
                List<uint> assetIds = new List<uint>();

                while (reader.Read())
                {
                    assetIds.Add(Convert.ToUInt32(reader["asset_id"]));
                }
                reader.Close();
                uint[] randAssetIds = GetRandAssetIds(assetIds, max);

                // new we get the players
                if (randAssetIds.Length == 0) return new List<Player>();
                cmd.CommandText = $"SELECT * FROM players WHERE asset_id IN({string.Join(",", randAssetIds)}) " +
                                  $"LIMIT 0, {max}";
                reader = cmd.ExecuteReader();
                return Factories.MakePlayers(reader);
            }
        }

        public static void LogTransaction(string account, uint buyNowPrice, int rating, long assetId, int tradePileCount,
            string type, string server, Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "INSERT INTO log (email, buy_now_price, rating, asset_id, trade_pile, type, server, platform) " +
                    "VALUES (@email, @buyNowPrice, @rating, @assetId, @tradePileCount, @type, @server, @platform)";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@email", account);
                cmd.Parameters.AddWithValue("@buyNowPrice", buyNowPrice);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.Parameters.AddWithValue("@assetId", assetId);
                cmd.Parameters.AddWithValue("@tradePileCount", tradePileCount);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@server", server);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        public static List<Player> GetPlatformPlayers(Platform platform, bool reverse)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();
                // we overwrite old gen
                Platform platformList = platform;
                switch (platformList)
                {
                    case Platform.Ps3:
                        platformList = Platform.Ps4;
                        break;
                    case Platform.Xbox360:
                        platformList = Platform.XboxOne;
                        break;
                }

                string ordering = reverse ? "DESC" : "ASC";
                cmd.CommandText = $"SELECT * FROM players WHERE platform='{platformList}' OR platform='All' " +
                                  $"ORDER BY asset_id {ordering} LIMIT 0, 2000";
                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakePlayers(reader);
            }
        }

        public static List<Player> GetAllPlayers()
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT * FROM players LIMIT 0, 4000";
                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakePlayers(reader);
            }
        }

        public static List<Player> GetAllCardWeightPlayers()
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT * FROM player_weights LIMIT 0, 8000";
                IDataReader reader = cmd.ExecuteReader();
                return Factories.MakeCardWeightPlayers(reader);
            }
        }

        public static uint GetPlayerPrice(long assetId, int rating, string platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = $"SELECT std_price{platform} as std_price FROM players " +
                                  "WHERE asset_id=@asset_id OR (base_id=@base_id AND rating=@rating) LIMIT 0, 1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.Parameters.AddWithValue("@base_id", assetId);
                cmd.Parameters.AddWithValue("@rating", rating);

                IDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    return Convert.ToUInt32(reader["std_price"]);
                }
                return 0;
            }
        }

        public static uint GetPlatformStock(Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT SUM(credits) as sum FROM panel " +
                                  "WHERE platform=@platform AND credits >= @credits LIMIT 0, 1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@credits", 10000 + Account.SmallAccount);

                IDataReader reader = cmd.ExecuteReader();

                uint sum = 0;
                while (reader.Read())
                {
                    sum = Convert.ToUInt32(reader["sum"]);
                }
                return sum;
            }
        }

        public static uint GetBinStock(Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT Max(credits) as max FROM panel WHERE platform=@platform LIMIT 0, 1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@platform", platform.ToString());

                IDataReader reader = cmd.ExecuteReader();

                uint max = 0;
                while (reader.Read())
                {
                    max = Convert.ToUInt32(reader["max"]) - Account.SmallAccount;
                }
                return max;
            }
        }

        public static uint GetAvgAccountStock(Platform platform, double average)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT Count(*) as count FROM panel WHERE platform=@platform " +
                                  "AND credits > @average LIMIT 0, 1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@average", average + Account.SmallAccount);

                IDataReader reader = cmd.ExecuteReader();

                uint count = 0;
                while (reader.Read())
                {
                    count = Convert.ToUInt32(reader["count"]);
                }
                return count;
            }
        }

        public static bool ShouldAddCards(Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "SELECT status FROM panel WHERE platform=@platform LIMIT 0, 1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@platform", platform.ToString());

                IDataReader reader = cmd.ExecuteReader();

                string status = string.Empty;
                while (reader.Read())
                {
                    status = (string) reader["status"];
                }

                return status.IndexOf("Working", StringComparison.Ordinal) != -1;
            }
        }

        public static void AddCard(Platform platform, OrderCard card, Account.CardStatuses status, uint item)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "INSERT INTO customer_cards (item, asset_id, trade_id, bin, start_price, value, platform, status) " +
                    "VALUES (@item, @asset_id, @trade_id, @bin, @start_price, @value, @platform, @status)";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@item", item);
                cmd.Parameters.AddWithValue("@asset_id", card.BaseId);
                cmd.Parameters.AddWithValue("@trade_id", card.TradeId);
                cmd.Parameters.AddWithValue("@bin", card.Bin);
                cmd.Parameters.AddWithValue("@start_price", card.StartPrice);
                cmd.Parameters.AddWithValue("@value", card.Value);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@status", status.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        private static uint[] GetRandAssetIds(List<uint> allAssetIds, byte max)
        {
            Random rand = new Random();
            max = max > allAssetIds.Count ? Convert.ToByte(allAssetIds.Count) : max;
            uint[] randAssetIds = new uint[max];

            for (int i = 0; i < max; i++)
            {
                int randI = rand.Next(0, allAssetIds.Count);
                randAssetIds[i] = allAssetIds[randI];
                allAssetIds.RemoveAt(randI);
            }

            return randAssetIds;
        }

        public static void SaveToPanel(string account, string server, Panel.Statuses status, string startedAt,
            uint credits, AppVersion login, Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "INSERT INTO panel (account, server, status, started_at, platform, credits, login) " +
                                  "VALUES (@account, @server, @status, @started_at, @platform, @credits, @login) ON DUPLICATE KEY " +
                                  "UPDATE account=@account, server=@server, status=@status, started_at=@started_at, " +
                                  "platform=@platform, credits=@credits, login=@login;";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@account", account);
                cmd.Parameters.AddWithValue("@server", server);
                cmd.Parameters.AddWithValue("@status", status + Program.Signature);
                cmd.Parameters.AddWithValue("@started_at", startedAt.ToString(CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@credits", credits);
                cmd.Parameters.AddWithValue("@login", login.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        public static void SavePlayerPrice(uint assetId, uint price, string platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = $"UPDATE players SET std_price{platform}=@price WHERE asset_id=@asset_id;";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void MarkErrorCard(Platform platform, string email, string message)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "UPDATE customer_cards SET status=@status1, message=@message " +
                    "WHERE status=@status2 AND platform=@platform AND account=@account LIMIT 1;";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@status1", Account.CardStatuses.Error.ToString());
                cmd.Parameters.AddWithValue("@status2", Account.CardStatuses.Locked.ToString());
                cmd.Parameters.AddWithValue("@account", email);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@message", message);
                cmd.ExecuteNonQuery();
            }
        }

        public static void MarkResetCard(Platform platform, string email, long tradeId)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "UPDATE customer_cards SET status=@status1, message=@message, account=@account2 " +
                    "WHERE status=@status2 AND platform=@platform AND account=@account " +
                    "AND trade_id=@trade_id LIMIT 1;";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@status1", Account.CardStatuses.New.ToString());
                cmd.Parameters.AddWithValue("@status2", Account.CardStatuses.Locked.ToString());
                cmd.Parameters.AddWithValue("@account", email);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@message", string.Empty);
                cmd.Parameters.AddWithValue("@account2", string.Empty);
                cmd.Parameters.AddWithValue("@trade_id", tradeId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void MarkBoughtCard(Platform platform, string email, long tradeId)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "UPDATE customer_cards SET status=@status1, message=@message " +
                    "WHERE status=@status2 AND platform=@platform AND account=@account " +
                    "AND trade_id=@trade_id LIMIT 1;";

                cmd.Prepare();
                cmd.Parameters.AddWithValue("@status1", Account.CardStatuses.Bought.ToString());
                cmd.Parameters.AddWithValue("@status2", Account.CardStatuses.Locked.ToString());
                cmd.Parameters.AddWithValue("@account", email);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                cmd.Parameters.AddWithValue("@message", string.Empty);
                cmd.Parameters.AddWithValue("@trade_id", tradeId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void SavePlayerCardWeight(uint assetId, int weight, string platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    $"UPDATE player_weights SET weight_{platform} = weight_{platform} + @weight WHERE asset_id=@asset_id;";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@weight", weight);
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void SavePlayerPriceF(uint assetId, string platform1, string platform2, uint price1, uint price2)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = $"UPDATE players SET std_price{platform1}=@price1, std_price{platform2}=@price2 " +
                                  "WHERE asset_id=@asset_id";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@price1", price1);
                cmd.Parameters.AddWithValue("@price2", price2);
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void Log(string message, string account, string server, string details)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "INSERT INTO exceptions (account, server, message, details) " +
                                  "VALUES (@account, @server, @message, @details);";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@message", message);
                cmd.Parameters.AddWithValue("@account", account);
                cmd.Parameters.AddWithValue("@server", server);
                cmd.Parameters.AddWithValue("@details", details);
                cmd.ExecuteNonQuery();
            }
        }

        public static string GetAccountStartedAt(string account, string server, Panel.Statuses status)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = $"SELECT started_at FROM panel WHERE account = '{account}' " +
                                  $"AND status = {status} AND server = {server} LIMIT 0,1;";

                IDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    return (string) reader["started_at"];
                }
            }
            return DateTime.Now.ToLongTimeString();
        }

        public static void SaveBaseId(int baseId, uint assetId, int rating)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "UPDATE players SET base_id=@base_id, rating=@rating WHERE asset_id=@asset_id ";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@base_id", baseId);
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.ExecuteNonQuery();
            }
        }

        public static void SaveNewPlayer(int baseId, string assetId, int rating, string commonName,
            string firstName, string lastName, uint stdPriceXboxOne, string type)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText =
                    "INSERT INTO player_weights (asset_id, base_id, common_name, first_name, last_name, rating, std_priceXboxOne, type) " +
                    "VALUES (@asset_id, @base_id, @common_name, @first_name, @last_name, @rating, @std_priceXboxOne, @type) " +
                    "ON DUPLICATE KEY UPDATE asset_id=@asset_id, base_id=@base_id, common_name=@common_name, type=@type, " +
                    "std_priceXboxOne=@std_priceXboxOne, first_name=@first_name, last_name=@last_name, rating=@rating;";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@base_id", baseId);
                cmd.Parameters.AddWithValue("@asset_id", assetId);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.Parameters.AddWithValue("@common_name", commonName);
                cmd.Parameters.AddWithValue("@first_name", firstName);
                cmd.Parameters.AddWithValue("@last_name", lastName);
                cmd.Parameters.AddWithValue("@std_priceXboxOne", stdPriceXboxOne);
                cmd.Parameters.AddWithValue("@type", type);

                cmd.ExecuteNonQuery();
            }
        }

        public static void SavePlayerStatistics(Player player, Dictionary<string, long> stats, Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();
                string profit = "p_l_" + platform.ToString().ToLower();
                string intp = "tp_" + platform.ToString().ToLower();

                if (platform == Platform.Ps4) // ps4 is the first platfrom where inserts are made
                {
                    DateTime addedOn = Convert.ToDateTime(player.AddedOn);
                    TimeSpan daysOn = DateTime.Now - addedOn;
                    cmd.CommandText =
                        "INSERT INTO player_statistics (name, rating, days_on, platform, asset_id, base_id) " +
                        "VALUES (@name, @rating, @days_on, @platform, @asset_id, @base_id) ";

                    cmd.Prepare();

                    string name = player.CommonName.Length != 0
                        ? player.CommonName
                        : player.FirstName + " " + player.LastName;

                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@rating", player.Rating);
                    cmd.Parameters.AddWithValue("@days_on", daysOn.Days);
                    cmd.Parameters.AddWithValue("@platform", player.PPlatform);
                    cmd.Parameters.AddWithValue("@asset_id", player.AssetId);
                    cmd.Parameters.AddWithValue("@base_id", player.BaseId);
                    cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                }

                cmd.CommandText =
                    $"UPDATE player_statistics SET {profit}=@{profit}, {intp}=@{intp} WHERE asset_id=@asset_id";
                cmd.Prepare();
                cmd.Parameters.AddWithValue($"@{profit}", stats["p_l"]);
                cmd.Parameters.AddWithValue($"@{intp}", stats["tp"]);
                cmd.Parameters.AddWithValue("@asset_id", player.AssetId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void ClearPlayerStatistics()
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                cmd.CommandText = "DELETE FROM player_statistics; ";
                cmd.ExecuteNonQuery();
            }
        }

        public static Dictionary<string, long> GetPlayerStatistics(uint assetId, uint baseId, byte rating,
            Platform platform)
        {
            using (MySqlConnection connection = new MySqlConnection(Cs.GetConnectionString(true)))
            using (MySqlCommand cmd = connection.CreateCommand())
            {
                connection.Open();

                // clossed stuff
                cmd.CommandText = "SELECT SUM(buy_now_price) as sum, COUNT(*) as count " +
                                  "FROM log WHERE type=@type AND platform=@platform " +
                                  "AND asset_id=@asset_id AND rating=@rating LIMIT 0,1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@type", Logger.Labels.Closed.ToString());
                cmd.Parameters.AddWithValue("@asset_id", baseId);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                IDataReader reader = cmd.ExecuteReader();

                long clossedSum = 0;
                long clossedCount = 0;
                while (reader.Read())
                {
                    try
                    {
                        clossedSum = reader["sum"] is DBNull ? 0 : Convert.ToInt64(reader["sum"]);
                        clossedCount = reader["count"] is DBNull ? 0 : Convert.ToInt64(reader["count"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                    break;
                }
                cmd.Parameters.Clear();
                reader.Close();

                // Bought Stuff
                cmd.CommandText = "SELECT SUM(buy_now_price) as sum, COUNT(*) as count " +
                                  "FROM log WHERE type=@type AND platform=@platform " +
                                  "AND asset_id=@asset_id AND rating=@rating LIMIT 0,1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@type", Logger.Labels.Bought.ToString());
                cmd.Parameters.AddWithValue("@asset_id", baseId);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                reader = cmd.ExecuteReader();

                long boughtSum = 0;
                long boughtCount = 0;
                while (reader.Read())
                {
                    try
                    {
                        boughtSum = reader["sum"] is DBNull ? 0 : Convert.ToInt64(reader["sum"]);
                        boughtCount = reader["count"] is DBNull ? 0 : Convert.ToInt64(reader["count"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                    break;
                }
                cmd.Parameters.Clear();
                reader.Close();

                // Bid Stuff
                cmd.CommandText = "SELECT SUM(buy_now_price) as sum, COUNT(*) as count " +
                                  "FROM log WHERE type=@type AND platform=@platform  " +
                                  "AND asset_id=@asset_id AND rating=@rating LIMIT 0,1";
                cmd.Prepare();
                cmd.Parameters.AddWithValue("@type", Logger.Labels.WonBid.ToString());
                cmd.Parameters.AddWithValue("@asset_id", baseId);
                cmd.Parameters.AddWithValue("@rating", rating);
                cmd.Parameters.AddWithValue("@platform", platform.ToString());
                reader = cmd.ExecuteReader();

                long wonbidSum = 0;
                long wonbidCount = 0;
                while (reader.Read())
                {
                    try
                    {
                        wonbidSum = reader["sum"] is DBNull ? 0 : Convert.ToInt64(reader["sum"]);
                        wonbidCount = reader["count"] is DBNull ? 0 : Convert.ToInt64(reader["count"]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                    break;
                }
                cmd.Parameters.Clear();
                reader.Close();

                clossedSum = Convert.ToInt64(clossedSum*0.95); // ea tax
                long profit = clossedSum - (wonbidSum + boughtSum);
                long intradepiles = wonbidCount + boughtCount - clossedCount;
                return new Dictionary<string, long>
                {
                    {"tp", intradepiles},
                    {"p_l", profit}
                };
            }
        }

        public static void SshConnect()
        {
            ConnectionInfo ci = new ConnectionInfo(DbHost, SshUser,
                new PasswordAuthenticationMethod(SshUser, SshPassword));
            Tunnel = new SshTunnel(ci, 3306);

            Cs = new MySqlConnectionStringBuilder
            {
                AllowBatch = true,
                Server = DbUserHost,
                Database = DbName,
                UserID = DbUser,
                Password = DbPassword,
                Port = checked((uint) Tunnel.LocalPort)
            };
        }
    }
}