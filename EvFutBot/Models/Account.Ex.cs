using System;
using System.Net.Http;
using System.Threading.Tasks;
using EvFutBot.Utilities;
using Newtonsoft.Json;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Models
{
    public partial class Account
    {
        public async Task HandleException(ArgumentException ex, int securityDelay, int runforHours, string email)
        {
            if (ShouldNotWork(_startedAt, runforHours))
            {
                return;
            }
            Disconnect();
            Logger.LogException(ex.Message, ex.ToString(), Email);
            var rand = new Random();
            var randDelay = rand.Next(securityDelay/1000, 1500);
            await Task.Delay(randDelay*1000);
            await LoginFut(false);
        }

        public async Task HandleException(ExpiredSessionException ex, int securityDelay, string email)
        {
            Disconnect();
            Logger.LogException(ex.Message, ex.ToString(), Email);

            var rand = new Random();
            var randDelay = rand.Next(securityDelay/1000, 1500);
            await Task.Delay(randDelay*1000);
            await LoginFut();
        }

        public async Task HandleException(CaptchaTriggeredException ex, string email)
        {
            Logger.LogException(ex.Message, ex.ToString(), Email);
            await Task.Delay(TimeSpan.FromMinutes(120));
        }

        public async Task HandleException(HttpRequestException ex, int securityDelay, string email)
        {
            // we don't log permission dennied and no trade exisits
            if (ex.Message.IndexOf("461", StringComparison.Ordinal) == -1
                && ex.Message.IndexOf("478", StringComparison.Ordinal) == -1)
            {
                Logger.LogException(ex.Message, ex.ToString(), Email);
                await Task.Delay(securityDelay);
            }
            // mobile workaround to missing ExpiredSessionException
            if (ex.Message.IndexOf("401", StringComparison.Ordinal) != -1)
            {
                Disconnect();
                var rand = new Random();
                var randDelay = rand.Next(securityDelay/1000, 1500);
                await Task.Delay(randDelay*1000);
                await LoginFut();
            }
        }

        public async Task HandleException(Exception ex, int securityDelay, string email)
        {
            await Task.Delay(securityDelay);
            Logger.LogException(ex.Message, ex.ToString(), email);
        }

        // rare eabug
        public async Task HandleException(ConflictException ex, int securityDelay, string email, AuctionInfo expiredCard)
        {
            try
            {
                await Task.Delay(6*1000);
                await _utClient.RemoveFromTradePileAsync(expiredCard);
            }
            catch (Exception)
            {
                // ignored
            }

            await Task.Delay(securityDelay);
            Logger.LogException(ex.Message, ex.ToString(), email);
        }

        // tradepile is full
        public async Task HandleException(JsonSerializationException ex, int securityDelay, string email)
        {
            await Task.Delay(securityDelay);
            Logger.LogException(ex.Message, ex.ToString(), email);
        }

        // tradepile is full
        public async Task HandleException(PermissionDeniedException ex, int securityDelay, string email)
        {
            await Task.Delay(securityDelay);
            Logger.LogException(ex.Message, ex.ToString(), email);
        }

        // ea stupid errors
        public void HandleException(PermissionDeniedException ex, int securityDelay, string email, bool error)
        {
            Logger.LogException(ex.Message, ex.ToString(), email);
        }
    }
}