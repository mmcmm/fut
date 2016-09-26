using System;
using System.Threading;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Constants;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Models
{
    public class Panel
    {
        public enum Statuses
        {
            Working,
            Stopped,
            MaxCredits
        }

        private readonly AppVersion _login;

        public Panel(string account, AppVersion login, Platform platform)
        {
            Account = account;
            Platform = platform;
            _login = login;
        }

        public Statuses Status { private get; set; }
        public uint Credits { private get; set; }
        private string Account { get; }
        public Platform Platform { get; set; }
        public string StartedAt { get; set; }

        public void Save()
        {
            try
            {
                Database.SaveToPanel(Account, Environment.MachineName, Status, StartedAt, Credits, _login, Platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.SaveToPanel(Account, Environment.MachineName, Status, StartedAt, Credits, _login, Platform);
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
                    Database.SaveToPanel(Account, Environment.MachineName, Status, StartedAt, Credits, _login, Platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.SaveToPanel(Account, Environment.MachineName, Status, StartedAt, Credits, _login,
                            Platform);
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

        public DateTime GetStartedAt()
        {
            try
            {
                string startedAt = Database.GetAccountStartedAt(Account, Environment.MachineName, Status);
                return Convert.ToDateTime(startedAt);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    string startedAt = Database.GetAccountStartedAt(Account, Environment.MachineName, Status);
                    return Convert.ToDateTime(startedAt);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return DateTime.Now;
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    string startedAt = Database.GetAccountStartedAt(Account, Environment.MachineName, Status);
                    return Convert.ToDateTime(startedAt);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        string startedAt = Database.GetAccountStartedAt(Account, Environment.MachineName, Status);
                        return Convert.ToDateTime(startedAt);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return DateTime.Now;
            }
        }
    }
}