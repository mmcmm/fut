using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Models;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Services
{
    public static class FutBin
    {
        private const string BaseUri = "http://www.futbin.com";
        private const string PlayerUriD = "/pages/player/graph.php?type=daily_graph&year=17&player=";
        private const string PlayerUriH = "/pages/player/graph.php?type=today&year=17&player=";
        private const string Referer = "http://www.futbin.com/";
        private const string Host = "www.futbin.com";

        private static readonly Platform[][] Platforms =
        {
            new[]
            {
                Platform.Ps4,
                Platform.XboxOne
            },
            new[]
            {
                Platform.Ps3,
                Platform.Xbox360
            }
        };

        // use this when add new players
        public static Task<bool> UpdatePrices()
        {
            return Task.Run(async () =>
            {
                // change to get all zero price players
                var players = Player.GetAllPlayers();
                foreach (var player in players)
                {
                    var i = 1;
                    var oldgen = false;
                    foreach (var platformGroup in Platforms)
                    {
                        if (i%2 == 0) oldgen = true;

                        await Task.Delay(2000);
                        uint xboxPrice = 0;
                        uint psPrice = 0;
                        var rawjson = await GetFutBinPrices(player.AssetId, PlayerUriH, oldgen);
                        var token = JToken.Parse(rawjson);
                        var xbox = token.SelectToken("xbox");
                        var ps = token.SelectToken("ps");

                        if (xbox.Any())
                            xboxPrice = Convert.ToUInt32(token.SelectToken("xbox").Last.Last);
                        if (ps.Any())
                            psPrice = Convert.ToUInt32(token.SelectToken("ps").Last.Last);

                        if (xboxPrice == 0 || psPrice == 0)
                        {
                            rawjson = await GetFutBinPrices(player.AssetId, PlayerUriD, oldgen);
                            token = JToken.Parse(rawjson);
                            xbox = token.SelectToken("xbox");
                            ps = token.SelectToken("ps");

                            if (xbox.Any() && xboxPrice == 0)
                                xboxPrice = Convert.ToUInt32(token.SelectToken("xbox").Last.Last);
                            if (ps.Any() && psPrice == 0)
                                psPrice = Convert.ToUInt32(token.SelectToken("ps").Last.Last);
                        }

                        SavePlayerPriceF(player, platformGroup, psPrice, xboxPrice);
                        i++;
                    }
                }
                return true;
            });
        }

        private static void SavePlayerPriceF(Player player, Platform[] platformGroup, uint psPrice, uint xboxPrice)
        {
            try
            {
                Database.SavePlayerPriceF(player.AssetId, platformGroup[0].ToString(),
                    platformGroup[1].ToString(), psPrice, xboxPrice);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SavePlayerPriceF(player.AssetId, platformGroup[0].ToString(),
                        platformGroup[1].ToString(), psPrice, xboxPrice);
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
                    Database.SavePlayerPriceF(player.AssetId, platformGroup[0].ToString(),
                        platformGroup[1].ToString(), psPrice, xboxPrice);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SavePlayerPriceF(player.AssetId, platformGroup[0].ToString(),
                            platformGroup[1].ToString(), psPrice, xboxPrice);
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

        // remove this with a get specific price  
        public static async Task<uint> GetFutBinXbOnePrice(uint assetId)
        {
            await Task.Delay(2000);
            uint xboxPrice = 0;

            var rawjson = await GetFutBinPrices(assetId, PlayerUriH, false);
            var token = JToken.Parse(rawjson);
            var xbox = token.SelectToken("xbox");

            if (xbox.Any())
                xboxPrice = Convert.ToUInt32(token.SelectToken("xbox").Last.Last);

            if (xboxPrice == 0)
            {
                rawjson = await GetFutBinPrices(assetId, PlayerUriD, false);
                token = JToken.Parse(rawjson);
                xbox = token.SelectToken("xbox");

                if (xbox.Any() && xboxPrice == 0)
                    xboxPrice = Convert.ToUInt32(token.SelectToken("xbox").Last.Last);
            }

            return xboxPrice;
        }

        public static async Task<string> GetFutBinPrices(uint assetId, string playerUri, bool oldgen)
        {
            using (var handler = new HttpClientHandler {UseCookies = false})
            using (var httpClient = new HttpClient(handler))
            {
                var authRequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(BaseUri + playerUri + assetId)
                };
                authRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                authRequest.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8", 0.7));
                authRequest.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-us", 0.5));
                authRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
                authRequest.Headers.Referrer = new Uri(Referer);
                authRequest.Headers.Host = Host;
                authRequest.Headers.Add("Cookie",
                    oldgen
                        ? "gentype=oldgen; platform=ps4; 17_field=full_sunny; xbox=true; ps=true; consoletype=ps4;"
                        : "gentype=newgen; platform=ps4; 17_field=full_sunny; xbox=true; ps=true; consoletype=ps4;");

                var result = await httpClient.SendAsync(authRequest);
                return await result.Content.ReadAsStringAsync();
            }
        }
    }
}