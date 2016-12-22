using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using Newtonsoft.Json;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Models
{
    public static class MmogaCustomer
    {
        private const string User = "msltdx0";
        private const string Key = "oongohR!ooY0aipaeKeg";
        private const string BaseUri = "https://www.mmoga.com";
        private const string PaymentEmail = "msltdx0@gmail.com";
        private const string PaymentCurrency = "USD";
        private const uint CoinAmount = 50000;
        private const string PaymentGateway = "skrill";
        private const uint MaxSell = 100; // we don't want to expose to much
        private const uint ItemId = 5097193;

        private static readonly Random Rand = new Random();

        public static Task<bool> AddCardsToBuy()
        {
            return Task.Run(async () =>
            {
                uint i = 0;
                while (true)
                {
                    var trade = await CheckXb360Demand();
                    if (trade?.Code == 200)
                    {
                        ProcessDemand(Platform.Xbox360, trade);
                        i++;
                    }

                    // delay between calls
                    await Task.Delay(Rand.Next(30, 90)*1000);
                    if (i >= MaxSell) break;
                }

                return true;
            });
        }


        // checks available orders
        private static async Task<MmogaTrade> CheckXb360Demand()
        {
            const string platform = "x360";
            var time = (uint) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var hash = GetHash(platform, time);
           
            var mmogaTradeRequest = new MmogaTradeRequest
            {
                user = User,
                platform = platform,
                coinAmount = CoinAmount,
                paymentGateway = PaymentGateway,
                paymentEmail = PaymentEmail,
                paymentCurrency = PaymentCurrency,
                time = time,
                hash = hash
            };
            var jsonreq = JsonConvert.SerializeObject(mmogaTradeRequest);
            var path = "/FIFA-Coins/FUT-Coins-Sell/?get_trade=" + WebUtility.UrlEncode(jsonreq);
            try
            {
                var jsonres = await Get(path, BaseUri);
                return jsonres.Length != 0 && jsonres != "[]" && jsonres != "{}"
                    ? JsonConvert.DeserializeObject<MmogaTrade>(jsonres)
                    : null;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return null;
            }
        }

        private static void ProcessDemand(Platform platform, MmogaTrade trade)
        {
            if (EvoCustomer.ShouldAddCards(platform) == false) return; // no account works
            if (EvoCustomer.GetPlatformStock(platform) <= CoinAmount*2) return; // we ballpark stock

            var card = new OrderCard
            {
                TradeId = trade.TradeId,
                BaseId = trade.AssetId,
                Bin = trade.CoinAmount,
                Value = 350,
                StartPrice = trade.MinPrice
            };

            EvoCustomer.AddCard(platform, card, Account.CardStatuses.New, ItemId);
        }

        private static string GetHash(string platform, uint time)
        {
            var message = "{0}|{1}|{2}|{3}|{4}|{5}";
            message = string.Format(message, platform, CoinAmount, PaymentGateway, PaymentEmail, PaymentCurrency, time);

            return BitConverter.ToString(HmacSha256(message, Key)).Replace("-", "").ToLower();
        }

        private static byte[] HmacSha256(string message, string key)
        {
            using (var hmac = new HMACRIPEMD160(Encoding.ASCII.GetBytes(key)))
            {
                return hmac.ComputeHash(Encoding.ASCII.GetBytes(message));
            }
        }

        public static async Task<string> Get(string path, string baseUrl)
        {
            using (var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            using (var httpClient = new HttpClient(handler))
            {
                httpClient.BaseAddress = new Uri(baseUrl);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.99 Safari/537.36");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1");

                var result = await httpClient.GetAsync(path);
                result.EnsureSuccessStatusCode();

                return await result.Content.ReadAsStringAsync();
            }
        }
    }
}