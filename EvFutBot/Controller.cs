using System;
using System.Net.Http;
using System.Threading.Tasks;
using EvFutBot.Models;
using EvFutBot.Utilities;
using UltimateTeam.Toolkit.Exceptions;

namespace EvFutBot
{
    public class Controller
    {
        private readonly Account _account;
        private readonly Panel _panel;
        private readonly Settings _settings;

        public Controller(Account account, Settings settings)
        {
            _settings = settings;
            _account = account;

            _panel = new Panel(account.Email, account.Login, account.Platform);
        }

        public async Task<bool> LoginAndWork()
        {
            var loginResponse = await _account.LoginFut();
            byte i = 1;
            while (loginResponse == null)
            {
                _account.Disconnect();
                var rand = new Random();
                var randDelay = rand.Next(60, 1500);
                await Task.Delay(randDelay*1000);
                loginResponse = await _account.LoginFut(i != 3); // we try without cookie

                if (i >= 6 && loginResponse == null) // we try 6 times
                {
                    Logger.LogException("To many login tries!", "", _account.Email);
                    return false;
                }
                i++;
            }
            // we logged in
            return await StartWorking();
        }

        public async Task<bool> StartWorking()
        {
            uint credits = 0;
            try
            {
                await Task.Delay(_settings.RmpDelay);
                credits = await _account.UpdateCredits();
            }
            catch (ExpiredSessionException ex)
            {
                await _account.HandleException(ex, _settings.SecurityDelay, _account.Email);
            }
            catch (ArgumentException ex)
            {
                await _account.HandleException(ex, _settings.SecurityDelay, _account.Email);
            }
            catch (CaptchaTriggeredException ex)
            {
                await _account.HandleException(ex, _account.Email);
            }
            catch (HttpRequestException ex)
            {
                await _account.HandleException(ex, _settings.SecurityDelay, _account.Email);
            }
            catch (Exception ex)
            {
                await _account.HandleException(ex, _settings.SecurityDelay, _account.Email);
            }

            _panel.Credits = credits;
            _panel.StartedAt = DateTime.Now.ToLongTimeString();
            if (credits >= _settings.MaxCredits)
            {
                _panel.Status = Panel.Statuses.MaxCredits;
                _panel.Save();
                return false;
            }
            _panel.Status = Panel.Statuses.Working;
            _panel.Save();

            switch (_account.Status)
            {
                case Account.Statuses.Coins:
                    try
                    {
                        await _account.MakeCoins(_settings, _panel);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString(), _account.Email);
                    }                  
                    break;
                case Account.Statuses.Prices:
                    try
                    {
                        await _account.UpdatePrices(_settings, _panel);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString(), _account.Email);
                    }
                    break;
                case Account.Statuses.List:
                    try
                    {
                        await _account.UpdateCardWeights(_settings, _panel);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString(), _account.Email);
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}