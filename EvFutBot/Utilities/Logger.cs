using System;
using System.Threading;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Models;

namespace EvFutBot.Utilities
{
    public static class Logger
    {
        public enum Labels
        {
            WonBid,
            Closed,
            Bought
        }

        public static void LogException(string message, string details = "", string account = "")
        {
            // we don't need to log to the db dev exceptiobs.
            if (Environment.MachineName == Program.DevMachine)
            {
                Console.WriteLine(message);
                Console.WriteLine(details);
                return;
            }

            try
            {
                Database.Log(message, account, Environment.MachineName, details);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.Log(message, account, Environment.MachineName, details);
                }
                catch (Exception ex)
                {
                    LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.Log(message, account, Environment.MachineName, details);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.Log(message, account, Environment.MachineName, details);
                    }
                    catch (Exception ex)
                    {
                        LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                LogException(ex.Message, ex.ToString());
            }
        }

        public static void ConsoleDebug(string message = "")
        {
            Console.WriteLine("{0} Time: {1}, ThreadId: {2}", message, DateTime.Now,
                Thread.CurrentThread.ManagedThreadId);
        }

        public static void LogTransaction(string account, uint buyNowPrice, int rating, long assetId, int tradePileCount,
            Labels type, Platform platform)
        {
            try
            {
                Database.LogTransaction(account, buyNowPrice, rating, assetId, tradePileCount, type.ToString(),
                    Environment.MachineName, platform);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    Database.LogTransaction(account, buyNowPrice, rating, assetId, tradePileCount, type.ToString(),
                        Environment.MachineName, platform);
                }
                catch (Exception ex)
                {
                    LogException(ex.Message, ex.ToString());
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.LogTransaction(account, buyNowPrice, rating, assetId, tradePileCount, type.ToString(),
                        Environment.MachineName, platform);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        Database.LogTransaction(account, buyNowPrice, rating, assetId, tradePileCount, type.ToString(),
                            Environment.MachineName, platform);
                    }
                    catch (Exception ex)
                    {
                        LogException(ex.Message, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex.Message, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                LogException(ex.Message, ex.ToString());
            }
        }
    }
}