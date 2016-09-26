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
            DevelopmentSearchParameters searchParameters = new DevelopmentSearchParameters
            {
                Page = page,
                DevelopmentType = DevelopmentType.Contract,
                Level = Level.Gold,
                MaxBid = maxPrice,
                PageSize = 15
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
            Credits = searchResponse.Credits;

            foreach (AuctionInfo auction in
                searchResponse.AuctionInfo.Where(auction => auction.ItemData.ResourceId == -2142482645))
            {
                if (auction.Expires <= settings.RmpDelay/1000/6 || auction.Expires >= 6*60) continue;
                uint nextbid = auction.CalculateBid();
                if (nextbid > maxPrice) continue;
                maxPrice = nextbid;
                if (maxPrice > Credits) continue;

                try
                {
                    await Task.Delay(Convert.ToInt32(settings.PreBidDelay));
                    AuctionResponse placeBid = await _utClient.PlaceBidAsync(auction, maxPrice);
                    if (placeBid.AuctionInfo == null) continue;
                    AuctionInfo boughtAction = placeBid.AuctionInfo.FirstOrDefault();

                    if (boughtAction != null && boughtAction.TradeState == "closed")
                    {
                        await Task.Delay(settings.RmpDelay);
                        SendItemToTradePileResponse tradePileResponse =
                            await _utClient.SendItemToTradePileAsync(boughtAction.ItemData);
                        TradePileItem tradeItem = tradePileResponse.ItemData.FirstOrDefault();

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
    }
}