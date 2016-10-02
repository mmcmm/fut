using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Services;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;
using UltimateTeam.Toolkit.Services;
using Currency = UltimateTeam.Toolkit.Constants.Currency;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public const uint SmallAccount = 6000;
        private const byte TradePileMax = 30;
        private const byte WatchListMax = 50;
        private const int QuickSellLimit = 900;

        private readonly string _cookie;
        private readonly string _gpassword; // gmail password
        private readonly uint _id;
        private readonly string _password;
        private readonly string _utSecurityAnswer;
        public readonly string Email;
        public readonly string Gmail;
        public readonly Platform Platform;
        private DateTime _startedAt;
        private FutClient _utClient;

        public Account(uint id, string email, string password, string utSecurityAnswer, string platform,
            string gmail, string gpassword, string status, AppVersion logins)
        {
            _id = id;
            Status = GetStatus(status.Trim());
            Email = email.Trim();
            _password = password.Trim();
            Gmail = gmail.Trim();
            _gpassword = gpassword.Trim();
            Login = logins;

            _utSecurityAnswer = utSecurityAnswer.Trim();
            Platform = GetToolkitPlatform(platform);
            _cookie = CookieUtil.Dir + "\\" + Login + "_" + Email + "_cookie.dat";
        }

        public AppVersion Login { get; set; }
        public uint Credits { get; private set; }
        public Statuses Status { get; private set; }

        private static Platform GetToolkitPlatform(string platform)
        {
            switch (platform)
            {
                case "Ps3":
                    return Platform.Ps3;
                case "Ps4":
                    return Platform.Ps4;
                case "XboxOne":
                    return Platform.XboxOne;
                case "Xbox360":
                    return Platform.Xbox360;
                case "Pc":
                    return Platform.Pc;
                default:
                    return Platform.Ps3;
            }
        }

        private static Statuses GetStatus(string status)
        {
            switch (status)
            {
                case "Coins":
                    return Statuses.Coins;
                case "List":
                    return Statuses.List;
                case "Banned":
                    return Statuses.Banned;
                case "Error":
                    return Statuses.Error;
                case "Flagged":
                    return Statuses.Flagged;
                case "Prices":
                    return Statuses.Prices;
                default:
                    return Statuses.Inactive;
            }
        }

        public async Task<LoginResponse> LoginFut(bool viacookie = true)
        {
            if (viacookie && File.Exists(_cookie))
            {
                var cookie = CookieUtil.ReadCookiesFromDisk(_cookie);
                _utClient = new FutClient(cookie);
            }
            else
            {
                _utClient = new FutClient();
            }
            try
            {
                ITwoFactorCodeProvider provider = new ImapTwoFactorCodeProvider(Gmail, _gpassword, Email);
                var loginDetails = new LoginDetails(Email, _password, _utSecurityAnswer, Platform, Login);
                var loginResponse = await _utClient.LoginAsync(loginDetails, provider);

                var cookiecontainer = _utClient.RequestFactories.CookieContainer;
                CookieUtil.DeleteCookieFromDisk(_cookie);
                CookieUtil.WriteCookiesToDisk(_cookie, cookiecontainer);
                return loginResponse;
            }
            catch (ExpiredSessionException ex)
            {
                var rand = new Random();
                var randDelay = rand.Next(60, 240);
                await HandleException(ex, randDelay, Email);
                return null;
            }
            catch (ArgumentException ex)
            {
                var rand = new Random();
                var randDelay = rand.Next(60, 240);
                await HandleException(ex, randDelay, 1, Email); // we try just 1 hour
                return null;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return null;
            }
            catch (HttpRequestException ex)
            {
                var rand = new Random();
                var randDelay = rand.Next(60, 240);
                await HandleException(ex, randDelay, Email);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString(), Email);
                return null;
            }
        }

        public async Task<uint> UpdateCredits()
        {
            Credits = Convert.ToUInt32((await _utClient.GetCreditsAsync()).Credits);
            return Credits;
        }

        public List<Player> GetPotentialPlayers(byte batch, byte maxCardCost)
        {
            try
            {
                return Database.GetPlayers(batch, GetMaxCardCost(Credits, maxCardCost), Platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetPlayers(batch, GetMaxCardCost(Credits, maxCardCost), Platform);
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
                    return Database.GetPlayers(batch, GetMaxCardCost(Credits, maxCardCost), Platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetPlayers(batch, GetMaxCardCost(Credits, maxCardCost), Platform);
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

        public Task<bool> MakeCoins(Settings settings, Panel panel)
        {
            return Task.Run(async () =>
            {
                _startedAt = Convert.ToDateTime(panel.StartedAt);

                // clear any unassigned purchased and tradepile
                Credits = await ClearTradePile(settings, _startedAt);
                await ClearUnassigned(settings);
                await ClearWatchList(settings, _startedAt);
                await ClearGiftList(settings);
                await ClearGiftPacks(settings);
                await BuyCustomerCards(settings, _startedAt);
                Credits = await ClearTradePile(settings, _startedAt);

                // we start with a clean-ish slate. 
                await ControlTradePile(settings, _startedAt);
                await ControlWatchList(settings, _startedAt);
                var players = GetPotentialPlayers(settings.Batch, settings.MaxCardCost);

                while (true) // main loop
                {
                    if (players.Count == 0) // we reset the loop
                    {
                        if (ShouldNotWork(_startedAt, settings.RunforHours)) // stop if working hours passed
                        {
                            await Task.Delay(settings.SecurityDelay*2); // we wait a litte for things to clear
                            Credits = await ClearTradePile(settings, _startedAt);
                            await ClearUnassigned(settings);
                            await ClearWatchList(settings, _startedAt);
                            await BuyCustomerCards(settings, _startedAt);
                            Credits = await ClearTradePile(settings, _startedAt);

                            Update(panel, Credits, Panel.Statuses.Stopped, settings.RmpDelay);
                            Disconnect();
                            return false;
                        }

                        await BuyCustomerCards(settings, _startedAt);
                        await ClearWatchList(settings, _startedAt);
                        Credits = await ClearTradePile(settings, _startedAt);
                        Update(panel, Credits, Panel.Statuses.Working, settings.RmpDelay);

                        // we get new players and run fitness round here.
                        await SearchAndBuyFitness(settings, _startedAt);
                        players = GetPotentialPlayers(settings.Batch, settings.MaxCardCost); // new players
                        while (players.Count == 0) // a fail safe
                        {
                            await Task.Delay(settings.SecurityDelay);
                            players = GetPotentialPlayers(settings.Batch, settings.MaxCardCost);
                        }
                    }
                     
                    if (Credits <= SmallAccount)
                    {
                        await SearchAndBuyFitness(settings, _startedAt); // a fitness round

                        var tradePileSize = await GetTradePileSize(settings);
                        var watchListSize = await GetWatchListSize(settings);
                      
                        // we must not lose control
                        if (watchListSize <= WatchListMax/5 && tradePileSize <= TradePileMax/3)
                        {
                            for (byte i = 1; i <= 6; i++) // we go over 6 pages
                            {
                                await Task.Delay(settings.RmpDelay);
                                await SearchAndBidPContracts(settings, _startedAt, i);
                            }
                        }
                        else
                        {
                            await Task.Delay(settings.SecurityDelay*4);
                            await ClearWatchList(settings, _startedAt);
                            Credits = await ClearTradePile(settings, _startedAt);
                        }
                    }
                    else
                    {
                        await SearchAndBuy(players.First(), settings, _startedAt);
                    }
                    players.RemoveAt(0);
                }
            });
        }

        public async Task<bool> SearchAndBuy(Player player, Settings settings, DateTime startedAt)
        {
            var stdPrice = player.GetStdPrice(Platform);
            var sellPrice = GetEaPrice(stdPrice, settings.SellPercent);
            var maxPrice = GetEaPrice(stdPrice, CalculatePercent(stdPrice, settings));
            var minPrice = GetEaPrice(CalculateMinPrice(maxPrice), 100);
            if (maxPrice > Credits) return false;

            AuctionResponse searchResponse;
            var prevBid = AuctionInfo.CalculatePreviousBid(maxPrice);
            var searchParameters = new PlayerSearchParameters
            {
                Page = 1,
                Level = player.Level,
                ResourceId = player.AssetId,
                MaxBuy = sellPrice, // we use sell price due to ea crazyness 
                MaxBid = prevBid,
                MinBuy = minPrice,
                PageSize = 15
            };

            try
            {
                await Task.Delay(settings.RmpDelay);
                searchResponse = await _utClient.SearchAsync(searchParameters);
                // we also sort them for buying
                searchResponse.AuctionInfo.Sort(
                    (x, y) => Convert.ToInt32(x.BuyNowPrice) - Convert.ToInt32(y.BuyNowPrice));
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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

            foreach (var auction in searchResponse.AuctionInfo.Where(
                auction => auction.ItemData.AssetId == player.BaseId && auction.ItemData.Rating == player.Rating))
            {
                if (auction.BuyNowPrice > maxPrice) break;
                maxPrice = auction.BuyNowPrice <= maxPrice ? auction.BuyNowPrice : maxPrice;
                if (maxPrice > Credits) continue;

                try
                {
                    await Task.Delay(settings.PreBidDelay);
                    var placeBid = await _utClient.PlaceBidAsync(auction, maxPrice);
                    if (placeBid.AuctionInfo == null) continue;
                    var boughtAction = placeBid.AuctionInfo.FirstOrDefault();

                    if (boughtAction != null && boughtAction.TradeState == "closed")
                    {
                        await Task.Delay(settings.RmpDelay);
                        var tradePileResponse = await _utClient.SendItemToTradePileAsync(boughtAction.ItemData);
                        var tradeItem = tradePileResponse.ItemData.FirstOrDefault();

                        if (tradeItem != null)
                        {
                            await Task.Delay(settings.RmpDelay);
                            await _utClient.ListAuctionAsync(new AuctionDetails(boughtAction.ItemData.Id,
                                GetAuctionDuration(startedAt, settings.RunforHours, Login),
                                CalculateBidPrice(sellPrice, settings.SellPercent), sellPrice));

                            Logger.LogTransaction(Email, boughtAction.ItemData.LastSalePrice,
                                boughtAction.ItemData.Rating, boughtAction.ItemData.AssetId,
                                tradePileResponse.ItemData.Count, Logger.Labels.Bought, Platform);
                        }
                    }

                    Credits = placeBid.Credits;
                    break; // bid/buy just once to avoid bans
                }
                catch (PermissionDeniedException)
                {
                    // ignored
                    break;
                }
                catch (NoSuchTradeExistsException)
                {
                    // ignored
                    break;
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            }
            return true;
        }

        public static bool ShouldNotWork(DateTime startedAt, int shouldRunFor)
        {
            var span = DateTime.Now.Subtract(startedAt);
            return Math.Abs(span.Hours) >= shouldRunFor;
        }

        public static void Update(Panel panel, uint credits, Panel.Statuses status, int rmpDelay)
        {
            panel.Credits = credits;
            panel.Status = status;
            panel.Save();
        }

        public async Task<bool> ControlTradePile(Settings settings, DateTime startedAt)
        {
            AuctionResponse tradePileList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                tradePileList = await _utClient.GetTradePileAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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

            // traepile to full, give it some time.
            while (tradePileList.AuctionInfo.Count >= TradePileMax/5)
            {
                await Task.Delay(settings.SecurityDelay*2);
                await ClearTradePile(settings, startedAt);

                // stop if working hours passed
                if (ShouldNotWork(startedAt, settings.RunforHours))
                {
                    return false;
                }

                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    tradePileList = await _utClient.GetTradePileAsync();
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            }
            return true;
        }

        public async Task<uint> ClearTradePile(Settings settings, DateTime startedAt)
        {
            AuctionResponse tradePileList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                tradePileList = await _utClient.GetTradePileAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return Credits;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                return Credits;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return Credits;
            }
            catch (HttpRequestException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return Credits;
            }
            catch (Exception ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return Credits;
            }

            if (tradePileList.AuctionInfo.Any(g => g.TradeState == "closed"))
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.RemoveSoldItemsFromTradePileAsync();
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                // we also log them
                foreach (var closedCard in tradePileList.AuctionInfo.Where(g => g.TradeState == "closed"))
                {
                    var price = closedCard.CurrentBid != 0 ? closedCard.CurrentBid : closedCard.BuyNowPrice;
                    Logger.LogTransaction(Email, price, closedCard.ItemData.Rating, closedCard.ItemData.AssetId,
                        tradePileList.AuctionInfo.Count, Logger.Labels.Closed, Platform);
                }
            }

            // clear any useless cards in the account that clog the tradepile
            string[] protectedTypes = {"player", "contract", "health"};
            foreach (var card in tradePileList.AuctionInfo.Where(
                card => (card.ItemData.ItemType == "player" && card.ItemData.Rating <= 70)
                        || !protectedTypes.Contains(card.ItemData.ItemType)))
            {
                try
                {
                    if (card.TradeState == "active")
                    {
                        continue;
                    }
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.QuickSellItemAsync(card.ItemData.Id);
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }

            // relist expired
            foreach (var expiredCard in tradePileList.AuctionInfo.Where(g => g.TradeState == "expired"))
            {
                try
                {
                    // we lower price if trade pile gets full 
                    var sellPrice = expiredCard.BuyNowPrice == 0
                        ? GetWonBidPrice(expiredCard.ItemData.AssetId, expiredCard.ItemData.LastSalePrice,
                            expiredCard.ItemData.Rating, settings.SellPercent)
                        : AuctionInfo.CalculatePreviousBid(expiredCard.BuyNowPrice);
                    // if the card doesn't sell we quick sell it
                    if (sellPrice <= expiredCard.ItemData.MarketDataMinPrice)
                    {
                        try
                        {
                            await Task.Delay(settings.RmpDelayLow);
                            await _utClient.QuickSellItemAsync(expiredCard.ItemData.Id);
                            // in case something goes wrong
                            sellPrice = AuctionInfo.CalculateNextBid(expiredCard.ItemData.MarketDataMinPrice);
                            continue;
                        }
                        catch (Exception)
                        {
                            //ignored
                        }
                    }
                    if (sellPrice < 250) sellPrice = 250; // contracts and cheap customer cards

                    await Task.Delay(settings.RmpDelay);
                    await _utClient.ListAuctionAsync(new AuctionDetails(expiredCard.ItemData.Id,
                        GetAuctionDuration(startedAt, settings.RunforHours, Login),
                        CalculateBidPrice(sellPrice, settings.SellPercent), sellPrice));
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ConflictException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email, expiredCard);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }

            // list won bids
            foreach (var wonBidCard in tradePileList.AuctionInfo.Where(g => g.TradeState == null))
            {
                try
                {
                    var sellPrice = GetWonBidPrice(wonBidCard.ItemData.AssetId, wonBidCard.ItemData.LastSalePrice,
                        wonBidCard.ItemData.Rating, settings.SellPercent);
                    var startPrice = CalculateBidPrice(sellPrice, settings.SellPercent);
                    // in case we can't get a reference price
                    if (sellPrice == 0 || sellPrice > wonBidCard.ItemData.MarketDataMaxPrice || startPrice < wonBidCard.ItemData.MarketDataMinPrice)
                    {
                        sellPrice = wonBidCard.ItemData.MarketDataMaxPrice;
                        startPrice = wonBidCard.ItemData.MarketDataMinPrice;
                    }
                    await Task.Delay(settings.RmpDelay);
                    await _utClient.ListAuctionAsync(new AuctionDetails(wonBidCard.ItemData.Id,
                        GetAuctionDuration(startedAt, settings.RunforHours, Login), startPrice, sellPrice));
                }
                catch (PermissionDeniedException ex)
                {
                    HandleException(ex, settings.SecurityDelay, Email, true);
                    break;
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }
            return tradePileList.Credits;
        }

        public async Task<bool> ClearUnassigned(Settings settings)
        {
            PurchasedItemsResponse unassignedList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                unassignedList = await _utClient.GetPurchasedItemsAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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

            foreach (var purchasedCard in unassignedList.ItemData)
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.SendItemToTradePileAsync(purchasedCard);

                    Logger.LogTransaction(Email, purchasedCard.LastSalePrice, purchasedCard.Rating,
                        purchasedCard.AssetId, unassignedList.ItemData.Count, Logger.Labels.WonBid, Platform);
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }
            return true;
        }

        public async Task<bool> ControlWatchList(Settings settings, DateTime startedAt)
        {
            WatchlistResponse watchList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                watchList = await _utClient.GetWatchlistAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            // watchlist to full, give it some time.
            while (watchList.AuctionInfo.Count >= WatchListMax/5)
            {
                // if room in tradepile
                if (await GetTradePileSize(settings) < TradePileMax/1.2)
                {
                    await Task.Delay(settings.SecurityDelay*2);
                    await ClearWatchList(settings, startedAt);
                }
                await Task.Delay(settings.SecurityDelay*2);
                await ClearTradePile(settings, startedAt);

                // stop if working hours passed
                if (ShouldNotWork(startedAt, settings.RunforHours))
                {
                    return false;
                }

                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    watchList = await _utClient.GetWatchlistAsync();
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    return false;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            }
            return true;
        }

        public async Task<bool> ClearWatchList(Settings settings, DateTime startedAt)
        {
            WatchlistResponse watchList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                watchList = await _utClient.GetWatchlistAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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

            // remove expired/outbid cards
            var outbidAuctions =
                watchList.AuctionInfo.Where(watchCard => watchCard.BidState == "outbid").ToList();
            if (outbidAuctions.Count != 0)
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.RemoveFromWatchlistAsync(outbidAuctions);
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
            }

            // move won bid items and skip bugged cards.
            foreach (var watchCard in watchList.AuctionInfo.Where(
                watchCard => watchCard.TradeState == "closed" && watchCard.BidState != "outbid")
                .Where(watchCard => !BuggedCardsWl.Contains(watchCard.ItemData.Id)))
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.SendItemToTradePileAsync(watchCard.ItemData);

                    Logger.LogTransaction(Email, watchCard.CurrentBid, watchCard.ItemData.Rating,
                        watchCard.ItemData.AssetId, watchList.AuctionInfo.Count, Logger.Labels.WonBid, Platform);
                }
                catch (JsonSerializationException ex) // tradepile is full or bugged cards.
                {
                    if (await GetTradePileSize(settings) >= TradePileMax*0.80)
                    {
                        await Task.Delay(settings.RmpDelay*15);
                        break;
                    }
                    await HandleException(ex, settings.SecurityDelay, Email);
                    BuggedCardsWl.Add(watchCard.ItemData.Id);
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }

            // remove bugged expired cards that doesn't show outbid
            var expiredAuctionsNone = watchList.AuctionInfo.Where(
                watchCard => watchCard.BidState == "none" && watchCard.TradeState == "expired").ToList();
            if (expiredAuctionsNone.Count != 0)
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.RemoveFromWatchlistAsync(expiredAuctionsNone);
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                }
            }

            // bugged items. 
            if (watchList.AuctionInfo.Any(
                watchCard => watchCard.ItemData.ItemState == "invalid" && watchCard.TradeState == "expired"))
            {
                Logger.LogException("Invalid item(s)!", "", Email);
            }
            // move the overflow cards
            foreach (var watchCard in watchList.AuctionInfo.Where(
                watchCard => watchCard.TradeState == "expired" && watchCard.ItemData.ItemState != "invalid"
                             && watchCard.BidState != "none"))
            {
                try
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.SendItemToTradePileAsync(watchCard.ItemData);
                }
                catch (JsonSerializationException ex) // tradepile is full or bugged cards
                {
                    if (await GetTradePileSize(settings) >= TradePileMax*0.80)
                    {
                        await Task.Delay(settings.RmpDelay*15);
                        break;
                    }
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ExpiredSessionException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (ArgumentException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                    break;
                }
                catch (CaptchaTriggeredException ex)
                {
                    await HandleException(ex, Email);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
                catch (Exception ex)
                {
                    await HandleException(ex, settings.SecurityDelay, Email);
                    break;
                }
            }
            return true;
        }

        public async Task<bool> ClearGiftList(Settings settings)
        {
            // not implemented in mobile app
            if (Login == AppVersion.CompanionApp) return false;

            try
            {
                await Task.Delay(settings.RmpDelayLow);
                var giftsResponse = await _utClient.GetGiftsListAsync();
                foreach (var gift in giftsResponse.ActiveMessage)
                {
                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.GetGiftAsync(gift.Id);
                }
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            return true;
        }

        public async Task<bool> ClearGiftPacks(Settings settings)
        {
            // not implemented in mobile app
            if (Login == AppVersion.CompanionApp) return false;

            try
            {
                await Task.Delay(settings.RmpDelayLow);
                var storeResponse = await _utClient.GetPackDetailsAsync();
                foreach (var pacDetails in storeResponse.Purchase.Where(p => p.Coins == 0 && p.FifaCashPrice == 0)
                    .Select(packDetail => new PackDetails(packDetail.Id, Currency.MTX, 0, true)))
                {
                    await Task.Delay(settings.RmpDelayLow);
                    var buyPackResponse = await _utClient.BuyPackAsync(pacDetails);

                    await Task.Delay(settings.RmpDelayLow);
                    await _utClient.QuickSellItemAsync(buyPackResponse.ItemIdList);
                }
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return false;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
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
            return true;
        }

        public async Task<int> GetTradePileSize(Settings settings)
        {
            AuctionResponse tradePileList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                tradePileList = await _utClient.GetTradePileAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return TradePileMax;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                return TradePileMax;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return TradePileMax;
            }
            catch (HttpRequestException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return TradePileMax;
            }
            catch (Exception ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return TradePileMax;
            }
            return tradePileList.AuctionInfo.Count;
        }

        public async Task<int> GetWatchListSize(Settings settings)
        {
            WatchlistResponse watchList;
            try
            {
                await Task.Delay(settings.RmpDelayLow);
                watchList = await _utClient.GetWatchlistAsync();
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return WatchListMax;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                return WatchListMax;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return WatchListMax;
            }
            catch (HttpRequestException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return WatchListMax;
            }
            catch (Exception ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return WatchListMax;
            }
            return watchList.AuctionInfo.Count;
        }

        public void Disconnect()
        {
            _utClient = null;
        }
    }
}