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
        private static List<Account> _accountsInWork;
        public static string Signature = "F"; // we use to make all servers updated
        public static string DevMachine = "DESKTOP-3A254DD";
        public static string WorkMachine = "WIN-76FUKLJMOIP";

        private static void Main()
        {
            if (Environment.MachineName == DevMachine)
            {
//                InitUpdateBaseIds();
//                InitStatistics();
//                InitEvoCustomerCards();
//                InitMmogaCustomerCards();
                InitAccounts(AppVersion.WebApp);
            }
            else
            {
                try
                {
                    _scheduler = new StdSchedulerFactory(new NameValueCollection
                    {
                        {"quartz.scheduler.instanceName", "MainScheduler"},
                        {"quartz.threadPool.threadCount", "7"}, // change when adding jobs
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

                    var statisticsjob = JobBuilder.Create<StatisticsJob>()
                        .WithIdentity("statisticsjob", "group1")
                        .Build();

                    var resetcardsperhourjob = JobBuilder.Create<ResetCardsPerHourJob>()
                        .WithIdentity("resetcardsperhourjob", "group1")
                        .Build();

                    var mmogaaddcardsjob = JobBuilder.Create<MmogaAddCardsJob>()
                        .WithIdentity("mmogaaddcardsjob", "group1")
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
                            .DailyAtHourAndMinute(18, 30) // 18, 30 - 24 hours format 
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var closeapptrigger = TriggerBuilder.Create()
                        .WithIdentity("closeapptrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(06, 14) // 06, 14 - 24 hours format 
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var evoaddcardstrigger = TriggerBuilder.Create()
                        .WithIdentity("evoaddcardstrigger", "group1")
                        .WithSimpleSchedule(x => x
                            .WithIntervalInMinutes(15) // every 15 min
                            .RepeatForever())
                        .Build();

                    var statisticstrigger = TriggerBuilder.Create()
                        .WithIdentity("statisticstrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(18, 15) // 17, 45 - 24 hours format 15 min before mobile
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    var resetcardsperhourtrigger = TriggerBuilder.Create()
                        .WithIdentity("resetcardsperhourtrigger", "group1")
                        .WithSimpleSchedule(x => x
                            .WithIntervalInMinutes(60) // every 60 min
                            .RepeatForever())
                        .Build();

                    var mmogaaddcardstrigger = TriggerBuilder.Create()
                        .WithIdentity("mmogaaddcardstrigger", "group1")
                        .WithSchedule(CronScheduleBuilder
                            .DailyAtHourAndMinute(09, 30) // 09, 30 - 24 hours format
                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")))
                        .Build();

                    _scheduler.ScheduleJob(webappjob, webapptrigger);
                    _scheduler.ScheduleJob(mobilejob, mobiletrigger);
                    _scheduler.ScheduleJob(closeappjob, closeapptrigger);
                    _scheduler.ScheduleJob(evoaddcardsjob, evoaddcardstrigger);
                    _scheduler.ScheduleJob(statisticsjob, statisticstrigger);
                    _scheduler.ScheduleJob(resetcardsperhourjob, resetcardsperhourtrigger);
//                    _scheduler.ScheduleJob(mmogaaddcardsjob, mmogaaddcardstrigger);
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
                // ramdom delay so we don't bomb the db server
                var randDelay0 = rand.Next(640);
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

            _accountsInWork = accounts;
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
            var settings = new Settings(new byte[] {10, 10}, new byte[] {5, 15}, 85, 100, 50, 10, 8000000, 120, 100, 3);
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

        private static void InitMmogaCustomerCards()
        {
            if (!DevOrWork()) return;
            if (Database.Tunnel == null) Database.SshConnect();

            var taskList = new List<Task>();
            var accountTask = new Task<bool>(() =>
                MmogaCustomer.AddCardsToBuy().Result);

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

        private static void InitResetCardsPerHour()
        {
            if (_accountsInWork == null) return;
            foreach (var account in _accountsInWork)
            {
                account.ResetCardsPerHour();
            }
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
                try
                {
                    Database.UpdatePanelToStopped();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }

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

        public class MmogaAddCardsJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitMmogaCustomerCards();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }

        public class StatisticsJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitStatistics();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }

        public class ResetCardsPerHourJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                try
                {
                    InitResetCardsPerHour();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                }
            }
        }
    }
}