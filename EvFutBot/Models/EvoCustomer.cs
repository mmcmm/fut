using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Models
{
    public static class EvoCustomer
    {
        private const string Apikey = "SwMHQQe8fqvgzs52DAvs7A7r";
        private const string BaseUri = "https://www.evoninja.com";
        private const int OlderThan = 1; // 1800 - 30 min check every 15 minutes.  

        public static Task<bool> AddCardsToBuy()
        {
            return Task.Run(async () =>
            {
                Demand demand = await CheckDemand();
                if (demand?.Ps3 != null)
                    foreach (Order order in demand.Ps3)
                    {
                        ProcessDemand(Platform.Ps3, order);
                    }
                if (demand?.Ps4 != null)
                    foreach (Order order in demand.Ps4)
                    {
                        ProcessDemand(Platform.Ps4, order);
                    }
                if (demand?.Xbox360 != null)
                    foreach (Order order in demand.Xbox360)
                    {
                        ProcessDemand(Platform.Xbox360, order);
                    }
                if (demand?.XboxOne != null)
                    foreach (Order order in demand.XboxOne)
                    {
                        ProcessDemand(Platform.XboxOne, order);
                    }
                return true;
            });
        }

        private static async void ProcessDemand(Platform platform, Order order)
        {
            if (order.Age < OlderThan) return; // we give a chance to pa guys  
            if (ShouldAddCards(platform) == false) return; // no account works
            if (GetPlatformStock(platform) <= order.Amt) return; // we ballpark stock

            IEnumerable<OrderCard> orderCards = await ReserveOrder(platform, order);
            List<OrderCard> cards = orderCards?.OrderBy(card => card.Bin).ToList();

            if (cards == null || cards.Count == 0 || cards.Any(card => card.TradeId == 0))
            {
                await CancelOrder(platform, order, "Error TradeId(s)");
                return;
            }

            if (GetBinStock(platform) <= cards.Last().Bin) // we only check for stock the highest card
            {
                await CancelOrder(platform, order, "Low Stock");
                return;
            }
            if (cards.Count != 1) // check average without first card)
            {
                List<OrderCard> avgcCards = cards.ToList(); // we clone it
                avgcCards.RemoveAt(cards.Count - 1);
                double average = avgcCards.Average(card => card.Bin);
                // we see if we have stock avg ballpark 
                if (average >= Account.SmallAccount*6 && GetAvgAccountStock(platform, average) <= avgcCards.Count)
                {
                    await CancelOrder(platform, order, "Low Stock");
                    return;
                }
            }

            Random rand = new Random();
            foreach (OrderCard card in cards)
            {
                int randDelay = rand.Next(60, 120);
                Thread.Sleep(randDelay*1000); // sleep 1-2 minutes                       
                AddCard(platform, card, Account.CardStatuses.New, order.Id);
            }

            await Task.Delay(240*1000); // sleep 4 minutes  
            await CompleteOrder(platform, order);
        }

        private static uint GetPlatformStock(Platform platform)
        {
            try
            {
                return Database.GetPlatformStock(platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetPlatformStock(platform);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetPlatformStock(platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetPlatformStock(platform);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return 0;
            }
        }

        private static uint GetBinStock(Platform platform)
        {
            try
            {
                return Database.GetBinStock(platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetBinStock(platform);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetBinStock(platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetBinStock(platform);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return 0;
            }
        }

        private static uint GetAvgAccountStock(Platform platform, double average)
        {
            try
            {
                return Database.GetAvgAccountStock(platform, average);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetAvgAccountStock(platform, average);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetAvgAccountStock(platform, average);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetAvgAccountStock(platform, average);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return 0;
            }
        }

        private static void AddCard(Platform platform, OrderCard card, Account.CardStatuses status, uint item)
        {
            try
            {
                Database.AddCard(platform, card, status, item);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.AddCard(platform, card, status, item);
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
                    Database.AddCard(platform, card, status, item);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.AddCard(platform, card, status, item);
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

        public static bool ShouldAddCards(Platform platform)
        {
            try
            {
                return Database.ShouldAddCards(platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.ShouldAddCards(platform);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return false;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.ShouldAddCards(platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.ShouldAddCards(platform);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return false;
            }
        }

        // checks available orders
        private static async Task<Demand> CheckDemand()
        {
            string path = $"/evoctrl/api/check/?key={Apikey}";
            try
            {
                string json = await Get(path);
                return json.Length != 0 && json != "[]"
                    ? JsonConvert.DeserializeObject<Demand>(json)
                    : null;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return null;
            }
        }

        // reserves an order
        private static async Task<IEnumerable<OrderCard>> ReserveOrder(Platform platform, Order order)
        {
            const string path = "/evoctrl/api/reserve/";
            string platformEvo = platform.ToString();
            if (platformEvo == "Ps4") platformEvo = "PS4";
            if (platformEvo == "Ps3") platformEvo = "PS3";

            FormUrlEncodedContent data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", Apikey),
                new KeyValuePair<string, string>("console", platformEvo),
                new KeyValuePair<string, string>("amt", Convert.ToString(order.Amt)),
                new KeyValuePair<string, string>("id", Convert.ToString(order.Id))
            });

            try
            {
                string json = await Post(path, data);
                return json.Length != 0 && json != "[]"
                    ? JsonConvert.DeserializeObject<IEnumerable<OrderCard>>(json)
                    : null;
            }
            catch (Exception ex)
            {
                await CancelOrder(platform, order, "JSON Error");
                Logger.LogException(ex.Message, ex.ToString());
                return null;
            }
        }

        // cancels an order
        private static async Task CancelOrder(Platform platform, Order order, string why = "")
        {
            const string path = "/evoctrl/api/cancel/";
            string platformEvo = platform.ToString();
            if (platformEvo == "Ps4") platformEvo = "PS4";
            if (platformEvo == "Ps3") platformEvo = "PS3";

            FormUrlEncodedContent data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", Apikey),
                new KeyValuePair<string, string>("console", platformEvo),
                new KeyValuePair<string, string>("id", Convert.ToString(order.Id)),
                new KeyValuePair<string, string>("why", why)
            });

            try
            {
                await Post(path, data);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        // completes an order
        private static async Task CompleteOrder(Platform platform, Order order)
        {
            const string path = "/evoctrl/api/complete/";
            string platformEvo = platform.ToString();
            if (platformEvo == "Ps4") platformEvo = "PS4";
            if (platformEvo == "Ps3") platformEvo = "PS3";

            FormUrlEncodedContent data = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", Apikey),
                new KeyValuePair<string, string>("console", platformEvo),
                new KeyValuePair<string, string>("id", Convert.ToString(order.Id)),
                new KeyValuePair<string, string>("amt", Convert.ToString(order.Amt)),
                new KeyValuePair<string, string>("paymentEmail", "prowholesale@outlook.com"),
                new KeyValuePair<string, string>("paymentMethod", "P")
            });

            try
            {
                byte i = 0;
                string result = await Post(path, data);
                while (result != "Successfully Submitted") // we try 6 times
                {
                    if (i == 6) break;
                    await Task.Delay(120*1000);
                    data = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("key", Apikey),
                        new KeyValuePair<string, string>("console", platformEvo),
                        new KeyValuePair<string, string>("id", Convert.ToString(order.Id)),
                        new KeyValuePair<string, string>("amt", Convert.ToString(order.Amt)),
                        new KeyValuePair<string, string>("paymentEmail", "prowholesale@outlook.com"),
                        new KeyValuePair<string, string>("paymentMethod", "P")
                    });
                    result = await Post(path, data);
                    i++;
                }

                if (result != "Successfully Submitted")
                {
                    Logger.LogException("Not Successfully Submitted!", $"{platformEvo}, Id: {order.Id}, {result}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
            }
        }

        private static async Task<string> Get(string path)
        {
            using (HttpClientHandler handler = new HttpClientHandler())
            using (HttpClient httpClient = new HttpClient(handler))
            {
                httpClient.BaseAddress = new Uri(BaseUri);
                HttpResponseMessage result = await httpClient.GetAsync(path);
                result.EnsureSuccessStatusCode();

                return await result.Content.ReadAsStringAsync();
            }
        }

        private static async Task<string> Post(string path, FormUrlEncodedContent data)
        {
            using (HttpClientHandler handler = new HttpClientHandler())
            using (HttpClient httpClient = new HttpClient(handler))
            {
                httpClient.BaseAddress = new Uri(BaseUri);
                HttpResponseMessage result = await httpClient.PostAsync(path, data).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                return await result.Content.ReadAsStringAsync();
            }
        }
    }
}