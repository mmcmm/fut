using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Models;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Models;
using Item = EvFutBot.Utilities.Item;

// won bids, Won BINs average bid, average BIN, average sold, In TradePiles, Profit/Loss

namespace EvFutBot.Services
{
    public static class PlayerStatistics
    {
        private const string DatabaseSearchUrl =
            "https://www.easports.com/fifa/ultimate-team/api/fut/item?jsonParamObject=";

        private static readonly Platform[] Platforms =
        {
            Platform.Ps4,
            Platform.XboxOne
        };

        public static Task<bool> CalculateAll()
        {
            return Task.Run(async () =>
            {
                ClearPlayerStatistics();
                List<Player> players = Player.GetAllPlayers();

                foreach (Platform platform in Platforms)
                {
                    foreach (Player player in players.Where(player => player.BaseId != 0))
                    {
                        await Task.Run(() =>
                        {
                            Dictionary<string, long> stats = GetPlayerStatistics(player.AssetId, player.BaseId,
                                player.Rating, platform);
                            if (stats != null) SavePlayerStatistics(player, stats, platform);
                        });
                    }
                }
                return true;
            });
        }

        public static Task<bool> UpdateBaseIds()
        {
            return Task.Run(async () =>
            {
                // we do this after you add players
                List<Player> players = Player.GetAllPlayers();
                foreach (Player player in players)
                {
                    try
                    {
                        await GetBaseId(player.AssetId);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
                return true;
            });
        }


        public static async Task GetBaseId(uint assetId)
        {
            PlayersSearchRequest results = await SearchDatabase(assetId);
            List<Item> items = results?.Items;
            if (items?[0].BaseId > 0)
            {
                SaveBaseId(items[0].BaseId, assetId, items[0].Rating);
            }
        }

        private static async Task<PlayersSearchRequest> SearchDatabase(uint assetId)
        {
            HttpClient dbClient = new HttpClient();
            string searchFor = "{\"id\":\"" + assetId + "\"}";
            HttpResponseMessage playersJson = await dbClient.GetAsync(DatabaseSearchUrl + searchFor);
            playersJson.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<PlayersSearchRequest>(await playersJson.Content.ReadAsStringAsync());
        }

        private static void SaveBaseId(int baseId, uint assetId, int rating)
        {
            try
            {
                Database.SaveBaseId(baseId, assetId, rating);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SaveBaseId(baseId, assetId, rating);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SaveBaseId(baseId, assetId, rating);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SaveBaseId(baseId, assetId, rating);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        private static Dictionary<string, long> GetPlayerStatistics(uint assetId, uint baseId, byte rating,
            Platform platform)
        {
            try
            {
                return Database.GetPlayerStatistics(assetId, baseId, rating, platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetPlayerStatistics(assetId, baseId, rating, platform);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetPlayerStatistics(assetId, baseId, rating, platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetPlayerStatistics(assetId, baseId, rating, platform);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
            return null;
        }

        private static void SavePlayerStatistics(Player player, Dictionary<string, long> stats, Platform platform)
        {
            try
            {
                Database.SavePlayerStatistics(player, stats, platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SavePlayerStatistics(player, stats, platform);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SavePlayerStatistics(player, stats, platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SavePlayerStatistics(player, stats, platform);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        private static void ClearPlayerStatistics()
        {
            try
            {
                Database.ClearPlayerStatistics();
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.ClearPlayerStatistics();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.ClearPlayerStatistics();
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.ClearPlayerStatistics();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }
    }
}