using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public Task<bool> UpdatePrices(Settings settings, Panel panel)
        {
            return Task.Run(async () =>
            {
                var startedAt = Convert.ToDateTime(panel.StartedAt);
                await ClearGiftList(settings);
                var reverse = _id > 4; // we get players in reverse

                var players = GetPlatformPlayers(Platform, reverse);

                while (true) // main loop
                {
                    if (players.Count == 0)
                    {
                        // stop if working hours passed
                        if (ShouldNotWork(startedAt, settings.RunforHours))
                        {
                            Update(panel, Credits, Panel.Statuses.Stopped, settings.RmpDelay);
                            Disconnect();
                            return false;
                        }
                        Update(panel, Credits, Panel.Statuses.Working, settings.RmpDelay);

                        players = GetPlatformPlayers(Platform, reverse);
                    }
                    if (players.Count == 0) continue;
                    await SearchAndPrice(players.First(), settings);
                    players.RemoveAt(0);
                }
            });
        }

        public static List<Player> GetPlatformPlayers(Platform platform, bool reverse)
        {
            try
            {
                return Database.GetPlatformPlayers(platform, reverse);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetPlatformPlayers(platform, reverse);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetPlatformPlayers(platform, reverse);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetPlatformPlayers(platform, reverse);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return new List<Player>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return new List<Player>();
            }
        }

        public async Task<bool> SearchAndPrice(Player player, Settings settings)
        {
            var maxPrice = GetEaPrice(player.GetStdPrice(Platform), 100);
            var lowestBinAvg = uint.MaxValue;

            AuctionResponse searchResponse;
            var searchParameters = new PlayerSearchParameters
            {
                Page = 1,
                ResourceId = player.AssetId,
                MaxBuy = maxPrice,
                Level = player.Level,
                PageSize = 15
            };

            try
            {
                await Task.Delay(settings.RmpDelayPrices);
                searchResponse = await _utClient.SearchAsync(searchParameters);
                while (searchResponse.AuctionInfo.Count == 16)
                {
                    searchParameters.MaxBuy = AuctionInfo.CalculatePreviousBid(searchParameters.MaxBuy);
                    await Task.Delay(settings.RmpDelayPrices);
                    searchResponse = await _utClient.SearchAsync(searchParameters);
                }
                while (searchResponse.AuctionInfo.Count < settings.LowestBinNr)
                {
                    searchParameters.MaxBuy = AuctionInfo.CalculateNextBid(searchParameters.MaxBuy);
                    await Task.Delay(settings.RmpDelayPrices);
                    searchResponse = await _utClient.SearchAsync(searchParameters);
                }
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return false;
            }
            catch (HttpRequestException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (Exception ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }

            searchResponse.AuctionInfo.Sort((x, y) => Convert.ToInt32(x.BuyNowPrice) - Convert.ToInt32(y.BuyNowPrice));
            for (var i = 0; i < settings.LowestBinNr; i++)
            {
                lowestBinAvg += searchResponse.AuctionInfo[i].BuyNowPrice;
            }

            lowestBinAvg = lowestBinAvg/settings.LowestBinNr;
            SavePlayerPrice(player.AssetId, lowestBinAvg, Platform.ToString());

            return true;
        }

        public void SavePlayerPrice(uint assetId, uint price, string platform)
        {
            try
            {
                Database.SavePlayerPrice(assetId, price, platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SavePlayerPrice(assetId, price, platform);
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
                    Database.SavePlayerPrice(assetId, price, platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SavePlayerPrice(assetId, price, platform);
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

        public static uint GetEaPrice(uint price, byte percent)
        {
            var maxBuy = price*percent/100;
            var roundTo = 50.0;
            if (maxBuy >= 1000) roundTo = 100.0;
            if (maxBuy >= 10000) roundTo = 250.0;
            if (maxBuy >= 50000) roundTo = 500.0;
            if (maxBuy >= 100000) roundTo = 1000.0;

            return Convert.ToUInt32(Math.Round(maxBuy/roundTo)*roundTo);
        }

        public static uint GetMaxCardCost(uint credits, byte maxCardCostPercent)
        {
            if (credits <= SmallAccount) maxCardCostPercent = 100; // exception for small accounts
            var playerCost = credits*maxCardCostPercent/100;
            var minCardCost = GetEaPrice(SmallAccount, maxCardCostPercent);

            return playerCost < minCardCost ? minCardCost : playerCost;
        }

        private uint GetWonBidPrice(long assetId, uint lastSalePrice, int rating, byte sellPercent)
        {
            // for player contracts
            if (lastSalePrice == 150) return 250;

            var playerPrice = GetPlayerPrice(assetId, rating, Platform);
            return playerPrice > 0
                ? GetEaPrice(playerPrice, sellPercent)
                : GetEaPrice(lastSalePrice, Convert.ToByte(sellPercent + 25));
        }

        private static uint GetPlayerPrice(long assetId, int rating, Platform platform)
        {
            try
            {
                return Database.GetPlayerPrice(assetId, rating, platform.ToString());
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetPlayerPrice(assetId, rating, platform.ToString());
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
                    return Database.GetPlayerPrice(assetId, rating, platform.ToString());
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetPlayerPrice(assetId, rating, platform.ToString());
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

        public static AuctionDuration GetAuctionDuration(DateTime startedAt, int shouldRunFor,
            AppVersion login)
        {
            var span = DateTime.Now.Subtract(startedAt);
            return Math.Abs(span.Hours) >= shouldRunFor - 2
                ? (login == AppVersion.CompanionApp ? AuctionDuration.SixHours : AuctionDuration.OneHour)
                : AuctionDuration.OneHour;
        }

        public static uint CalculateBidPrice(uint binprice, byte percent, byte less = 0)
        {
            uint bidprice;
            if (less == 0)
            {
                bidprice = AuctionInfo.CalculatePreviousBid(binprice);
                return bidprice == 0 ? 150 : bidprice;
            }

            percent -= less;
            bidprice = GetEaPrice(binprice, percent);
            var prevBidPrice = AuctionInfo.CalculatePreviousBid(binprice);
            prevBidPrice = prevBidPrice == 0 ? 150 : prevBidPrice;

            return bidprice == binprice ? prevBidPrice : bidprice;
        }

        public static uint CalculateMinPrice(uint maxPrice)
        {
            var rand = new Random();
            var minPrice = rand.Next(200, Convert.ToInt32(maxPrice/2));
            return Convert.ToUInt32(minPrice);
        }

        public static byte CalculatePercent(uint maxPrice, Settings settings)
        {
            var percent = settings.BinPercent;
            if (maxPrice >= 4600) percent += 5;

            return percent;
        }
    }
}