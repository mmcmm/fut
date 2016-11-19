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
                        _cardsPerHour++;
                        await Task.Delay(settings.RmpDelay);
                        var tradePileResponse =
                            await _utClient.SendItemToTradePileAsync(boughtAction.ItemData);
                        var tradeItem = tradePileResponse.ItemData.FirstOrDefault();

                        if (tradeItem != null)
                        {                     
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
    }
}