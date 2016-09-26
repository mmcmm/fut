using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Renci.SshNet.Common;

namespace EvFutBot.Services
{
    public class PlayerWeights
    {
        private const string DatabaseSearchUrl =
            "https://www.easports.com/fifa/ultimate-team/api/fut/item?jsonParamObject=";

        private const uint PriceMin = 900;
        private const uint PriceMax = 10500;

        public static string[] CardTypes =
        {
            "rare_gold"
        };

        public static Task<bool> AddAll()
        {
            return Task.Run(async () =>
            {
                try
                {
                    await GetNewPlayers();
                }
                catch (Exception)
                {
                    // ignored
                }
                return true;
            });
        }

        public static async Task GetNewPlayers()
        {
            var results = await SearchDatabase(1);
            var items = results?.Items;
            for (uint i = 1; i <= results?.TotalPages; i++)
            {
                if (items != null)
                {
                    foreach (var item in items.Where(item => CardTypes.Contains(item.Color)))
                    {
                        var price = await FutBin.GetFutBinXbOnePrice(Convert.ToUInt32(item.Id));
                        if (price >= PriceMin && price <= PriceMax)
                        {
                            SaveNewPlayer(item.BaseId, item.Id, item.Rating, item.CommonName, item.FirstName,
                                item.LastName, price, item.Color);
                        }
                    }
                }
                results = await SearchDatabase(i + 1);
                items = results?.Items;
            }
        }

        private static async Task<PlayersSearchRequest> SearchDatabase(uint page)
        {
            var dbClient = new HttpClient();
            var pageNr = "{\"page\":\"" + page + "\"}";
            var playersJson = await dbClient.GetAsync(DatabaseSearchUrl + pageNr);
            playersJson.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<PlayersSearchRequest>(await playersJson.Content.ReadAsStringAsync());
        }

        private static void SaveNewPlayer(int baseId, string assetId, int rating, string commonName,
            string firstName, string lastName, uint stdPriceXboxOne, string type)
        {
            try
            {
                Database.SaveNewPlayer(baseId, assetId, rating, commonName, firstName, lastName,
                    stdPriceXboxOne, type);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SaveNewPlayer(baseId, assetId, rating, commonName, firstName, lastName,
                        stdPriceXboxOne, type);
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
                    Database.SaveNewPlayer(baseId, assetId, rating, commonName, firstName, lastName,
                        stdPriceXboxOne, type);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SaveNewPlayer(baseId, assetId, rating, commonName, firstName, lastName,
                            stdPriceXboxOne, type);
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