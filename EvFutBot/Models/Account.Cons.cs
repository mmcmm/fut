using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public const uint FitnessTeamDefId = 5002006;
        public const int FitnessResourceId = -1874046186;

        public async Task<bool> SearchAndBidPContracts(Settings settings, DateTime startedAt, byte page)
        {
            const int resourceId = -1874047186;
            const uint definitionId = 5001006;
            const uint sellPrice = 250;
            uint maxPrice = 150;
            if (maxPrice > Credits) return false;

            AuctionResponse searchResponse;
            var searchParameters = new DevelopmentSearchParameters
            {
                Page = page,
                DevelopmentType = DevelopmentType.Contract,
                Level = Level.Gold,
                MaxBid = maxPrice,
                PageSize = 15,
                DefinitionId = definitionId
            };

            try
            {
                await Task.Delay(settings.RmpDelay);
                searchResponse = await _utClient.SearchAsync(searchParameters);
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

            foreach (var auction in searchResponse.AuctionInfo
                .Where(auction => auction.ItemData.ResourceId == resourceId))
            {
                if (auction.Expires >= 6*60) continue;
                var nextbid = auction.CalculateBid();
                if (nextbid > maxPrice) continue;
                maxPrice = nextbid;
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
                        var tradePileResponse =
                            await _utClient.SendItemToTradePileAsync(boughtAction.ItemData);
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
                    break; // bid just once to avoid bans
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
            }
            return true;
        }

        public async Task<bool> SearchAndBuyFitness(Settings settings, DateTime startedAt)
        {           
            var fitnessStdPrice = GetConsumablePrice(Platform, FitnessTeamDefId);
            if (fitnessStdPrice > Credits) return false;

            var sellPrice = GetEaPrice(fitnessStdPrice, Convert.ToByte(settings.SellPercent));          
            var maxPrice = GetEaPrice(fitnessStdPrice, settings.BinPercent);
            var minPrice = GetEaPrice(CalculateMinPrice(maxPrice), 100);
            if (maxPrice > Credits) return false;

            AuctionResponse searchResponse;
            var prevBid = AuctionInfo.CalculatePreviousBid(maxPrice);
            var searchParameters = new DevelopmentSearchParameters
            {
                Page = 1,
                DevelopmentType = DevelopmentType.Fitness,
                Level = Level.Gold,
                MaxBuy = sellPrice, // we use sell price due to ea crazyness 
                MaxBid = prevBid,
                MinBuy = minPrice,
                PageSize = 15,
                DefinitionId = FitnessTeamDefId
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

            foreach (var auction in searchResponse
                .AuctionInfo.Where(auction => auction.ItemData.ResourceId == -1874046186))
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
                            // I guess we can be a little greedy
                            sellPrice = AuctionInfo.CalculateNextBid(sellPrice);

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
            }
            return true;
        }

        public uint GetConsumablePrice(Platform platform, uint definitionId)
        {
            try
            {
                return Database.GetConsumablePrice(definitionId, platform.ToString());
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetConsumablePrice(definitionId, platform.ToString());
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 150;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetConsumablePrice(definitionId, platform.ToString());
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetConsumablePrice(definitionId, platform.ToString());
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return 150;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return 150;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return 150;
            }
        }
    }
}