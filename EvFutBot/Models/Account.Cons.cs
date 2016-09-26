using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public async Task<bool> SearchAndBidPContracts(Settings settings, DateTime startedAt, byte page)
        {
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
                DefinitionId = 5001006
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

            foreach (var auction in searchResponse.AuctionInfo
                .Where(auction => auction.ItemData.ResourceId == -1874047186))
            {
                if (auction.Expires <= settings.RmpDelay/1000/6 || auction.Expires >= 6*60) continue;
                var nextbid = auction.CalculateBid();
                if (nextbid > maxPrice) continue;
                maxPrice = nextbid;
                if (maxPrice > Credits) continue;

                try
                {
                    await Task.Delay(Convert.ToInt32(settings.PreBidDelay));
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
                            await
                                _utClient.ListAuctionAsync(new AuctionDetails(boughtAction.ItemData.Id,
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


        public async Task<bool> SearchAndBuyFitness(Settings settings, DateTime startedAt)
        {
//            uint fitnessStdPrice = GetConsumablePrice(Platform, definitionId);
            uint fitnessStdPrice = 900;
            var sellPrice = GetEaPrice(fitnessStdPrice, settings.SellPercent);
            var maxPrice = GetEaPrice(fitnessStdPrice, settings.BinPercent);
            if (maxPrice > Credits) return false;

            AuctionResponse searchResponse;
            var searchParameters = new DevelopmentSearchParameters
            {
                Page = 1,
                DevelopmentType = DevelopmentType.Fitness,
                Level = Level.Gold,
                MaxBuy = maxPrice,
                DefinitionId = 5002006
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

            searchResponse.AuctionInfo.Sort((x, y) => Convert.ToInt32(x.BuyNowPrice) - Convert.ToInt32(y.BuyNowPrice));

            foreach (var auction in searchResponse
                .AuctionInfo.Where(auction => auction.ItemData.ResourceId == -1874046186))
            {
                maxPrice = auction.BuyNowPrice <= maxPrice ? auction.BuyNowPrice : maxPrice;
                if (maxPrice > Credits) continue;

                try
                {
                    await Task.Delay(Convert.ToInt32(settings.PreBidDelay));
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
                            await
                                _utClient.ListAuctionAsync(new AuctionDetails(boughtAction.ItemData.Id,
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
    }
}