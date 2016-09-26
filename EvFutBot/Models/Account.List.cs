using System;
using System.Collections.Generic;
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
        public Task<bool> UpdateCardWeights(Settings settings, Panel panel)
        {
            return Task.Run(async () =>
            {
                DateTime startedAt = Convert.ToDateTime(panel.StartedAt);

                await ClearGiftList(settings);
                List<Player> players = Player.GetAllCardWeightPlayers();

                while (true) // main loop
                {
                    if (players.Count == 0 || ShouldNotWork(startedAt, settings.RunforHours + 1))
                        // we can run it for 1 more hour
                    {
                        // clear any unassigned purchased and tradepile
                        Credits = await ClearTradePile(settings, startedAt);
                        await ClearUnassigned(settings);
                        await ClearWatchList(settings, startedAt);
                        await ClearGiftList(settings);
                        Credits = await ClearTradePile(settings, startedAt);

                        Update(panel, Credits, Panel.Statuses.Stopped, settings.RmpDelay);
                        Disconnect();
                        return false;
                    }
                    if (players.Count == 0) continue;
                    await SearchAndGetCardWeight(players.First(), settings);
                    players.RemoveAt(0);
                }
            });
        }

        public async Task<int> SearchAndGetCardWeight(Player player, Settings settings)
        {
            int results = 0;
            PlayerSearchParameters searchParameters = new PlayerSearchParameters
            {
                Page = 1,
                ResourceId = player.AssetId,
                PageSize = 49
            };

            try
            {
                await Task.Delay(settings.RmpDelay);
                AuctionResponse searchResponse = await _utClient.SearchAsync(searchParameters);
                results += searchResponse.AuctionInfo.Count(c => c.ItemData.Rating == player.Rating);
                while (searchResponse.AuctionInfo.Count != 0)
                {
                    searchParameters.Page++;
                    await Task.Delay(settings.RmpDelay);
                    searchResponse = await _utClient.SearchAsync(searchParameters);
                    results += searchResponse.AuctionInfo.Count(c => c.ItemData.Rating == player.Rating);
                }
            }
            catch (ExpiredSessionException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return 0;
            }
            catch (ArgumentException ex)
            {
                await HandleException(ex, settings.SecurityDelay, settings.RunforHours, Email);
                return 0;
            }
            catch (CaptchaTriggeredException ex)
            {
                await HandleException(ex, Email);
                return 0;
            }
            catch (HttpRequestException ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return 0;
            }
            catch (Exception ex)
            {
                await HandleException(ex, settings.SecurityDelay, Email);
                return 0;
            }

            SavePlayerCardWeight(player.AssetId, results, Platform.ToString());
            return results;
        }

        public void SavePlayerCardWeight(uint assetId, int weight, string platform)
        {
            try
            {
                Database.SavePlayerCardWeight(assetId, weight, platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SavePlayerCardWeight(assetId, weight, platform);
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
                    Database.SavePlayerCardWeight(assetId, weight, platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SavePlayerCardWeight(assetId, weight, platform);
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