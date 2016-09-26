using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvFutBot.Models;
using EvFutBot.Services;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Quartz;
using Quartz.Impl;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Constants;

namespace EvFutBot
{
    internal class Program
    {
        private static IScheduler _scheduler;
        public static string Signature = "F"; // we use to make all servers updated
        public static string DevMachine = "DESKTOP-3A254DD";
        public static string WorkMachine = "WIN-76FUKLJMOIP";

        private static void Main()
        {
            if (Environment.MachineName == DevMachine)
            {
//                InitUpdateBaseIds();
//                InitStatistics();
//                InitAddNewPlayers();
//                InitEvoCustomerCards();
                InitAccounts(AppVersion.WebApp);
            }
            else
            {
                try
                {
                    _scheduler = new StdSchedulerFactory(new NameValueCollection
                    {
                        {"quartz.scheduler.instanceName", "MainScheduler"},
                        {"quartz.threadPool.threadCount", "4"}, // change when adding jobs
                        {"quartz.jobStore.type", "Quartz.Simpl.RAMJobStore, Quartz"}
                    }).GetScheduler();
                    _scheduler.Start();

                    var webappjob = JobBuilder.Create<WebAppJob>()
                        .WithIdentity("webappjob", "group1")
                        .Build();

                    var mobilejob = JobBuilder.Create<MobileJob>()
                        .WithIdentity("mobilejob", "group1")
                        .Build();

                    var closeappjob = JobBuilder.Create<CloseAppJob>()
                        .WithIdentity("closeappjob", "group1")
                        .Build();

                    var evoaddcardsjob = JobBuilder.Create<EvoAddCardsJob>()
                        .WithIdentity("evoaddcardsjob", "group1")
                        .Build();

                    var webapptrigger = TriggerBuilder.Create()
                        .WithIdentity("webapptrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(07, 30) // 07, 30 - 24 hours format
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var mobiletrigger = TriggerBuilder.Create()
                        .WithIdentity("mobiletrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(18, 00) // 18, 00 - 24 hours format 
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var closeapptrigger = TriggerBuilder.Create()
                        .WithIdentity("closeapptrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(03, 15) // 03, 15 - 24 hours format 
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var evoaddcardstrigger = TriggerBuilder.Create()
                        .WithIdentity("evoaddcardstrigger", "group1")
                        .WithSimpleSchedule(x => x
                            .WithIntervalInMinutes(15) // every 15 min
                            .RepeatForever())
                        .Build();

                    _scheduler.ScheduleJob(webappjob, webapptrigger);
//                    _scheduler.ScheduleJob(mobilejob, mobiletrigger); todo suport mobile
                    _scheduler.ScheduleJob(closeappjob, closeapptrigger);
                    _scheduler.ScheduleJob(evoaddcardsjob, evoaddcardstrigger);
                }
                catch (SchedulerException ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }

        private static void InitAccounts(AppVersion login)
        {
            var rand = new Random();
            if (Environment.MachineName != DevMachine)
            {
                var randDelay0 = rand.Next(480);
                Thread.Sleep(randDelay0*1000);
            }

            var settings = GetSettings();
            var accounts = GetAccounts(settings, login);

            byte i = 0;
            while (accounts.Count == 0)
            {
                Thread.Sleep(30*1000);
                accounts = GetAccounts(settings, login);
                if (i == 6)
                {
                    Logger.LogException("Can't get accounts!");
                    return;
                }
                i++;
            }

            var taskList = new List<Task>();
            foreach (var accountTask in accounts.Select(account => new Task<bool>(() =>
                new Controller(account, settings).LoginAndWork().Result, TaskCreationOptions.LongRunning)))
            {
                if (Environment.MachineName != DevMachine)
                {
                    var randDelay = rand.Next(60, 120);
                    Thread.Sleep(randDelay*1000);
                }
                accountTask.Start();
                taskList.Add(accountTask);
            }
            Task.WaitAll(taskList.ToArray());
        }

        private static List<Account> GetAccounts(Settings settings, AppVersion login)
        {
            List<Account> accounts;
            try
            {
                accounts = Database.GetAccounts(settings.MaxAccounts, login);
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    accounts = Database.GetAccounts(settings.MaxAccounts, login);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    accounts = new List<Account>();
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    accounts = Database.GetAccounts(settings.MaxAccounts, login);
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        accounts = Database.GetAccounts(settings.MaxAccounts, login);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        accounts = new List<Account>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    accounts = new List<Account>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                accounts = new List<Account>();
            }

            return accounts;
        }

        private static Settings GetSettings()
        {
            var settings = new Settings(new byte[] {9, 9}, new byte[] {10, 10}, 85, 100, 50, 15, 4000000, 120, 100,
                3);
            if (Database.Tunnel == null) Database.SshConnect();

            try
            {
                settings = Database.GetSettings();
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    settings = Database.GetSettings();
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
                    settings = Database.GetSettings();
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        settings = Database.GetSettings();
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
            return settings;
        }

        private static void InitEvoCustomerCards()
        {
            if (!DevOrWork()) return;
            if (Database.Tunnel == null) Database.SshConnect();

            var taskList = new List<Task>();
            var accountTask = new Task<bool>(() =>
                EvoCustomer.AddCardsToBuy().Result);

            accountTask.Start();
            taskList.Add(accountTask);
            Task.WaitAll(taskList.ToArray());
        }

        private static void InitStatistics()
        {
            if (!DevOrWork()) return;
            if (Database.Tunnel == null) Database.SshConnect();

            var taskList = new List<Task>();
            var accountTask = new Task<bool>(() =>
                PlayerStatistics.CalculateAll().Result);

            accountTask.Start();
            taskList.Add(accountTask);
            Task.WaitAll(taskList.ToArray());
        }

        private static void InitUpdateBaseIds()
        {
            if (!DevOrWork()) return;
            if (Database.Tunnel == null) Database.SshConnect();

            var taskList = new List<Task>();
            var accountTask = new Task<bool>(() =>
                PlayerStatistics.UpdateBaseIds().Result);

            accountTask.Start();
            taskList.Add(accountTask);
            Task.WaitAll(taskList.ToArray());
        }

        private static void InitAddNewPlayers()
        {
            if (!DevOrWork()) return;
            if (Database.Tunnel == null) Database.SshConnect();

            var taskList = new List<Task>();
            var accountTask = new Task<bool>(() =>
                PlayerWeights.AddAll().Result);

            accountTask.Start();
            taskList.Add(accountTask);
            Task.WaitAll(taskList.ToArray());
        }

        // we use 1 server for work stuff
        public static bool DevOrWork()
        {
            return Environment.MachineName == WorkMachine ||
                   Environment.MachineName == DevMachine;
        }

        public class WebAppJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitAccounts(AppVersion.WebApp);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }

        public class CloseAppJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                _scheduler.Shutdown();
            }
        }

        public class MobileJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitAccounts(AppVersion.CompanionApp);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }

        public class EvoAddCardsJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitEvoCustomerCards();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }
    }
}