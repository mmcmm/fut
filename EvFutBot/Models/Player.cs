using System;
using System.Collections.Generic;
using System.Threading;
using EvFutBot.Utilities;
using MySql.Data.MySqlClient;
using Renci.SshNet.Common;
using UltimateTeam.Toolkit.Models;
using UltimateTeam.Toolkit.Parameters;

namespace EvFutBot.Models
{
    public class Player
    {
        public enum Statuses
        {
            Active,
            Inactive
        }

        public Player(uint assetId, uint baseId, string commonName, string firstName, string lastName,
            byte rating, uint stdPricePs3, uint stdPricePs4, uint stdPriceXboxOne, uint stdPriceXbox360,
            string addedOn, string platform)
        {
            AssetId = assetId;
            BaseId = baseId;
            CommonName = commonName;
            FirstName = firstName;
            LastName = lastName;
            Rating = rating;
            StdPricePs3 = stdPricePs3;
            StdPricePs4 = stdPricePs4;
            StdPriceXboxOne = stdPriceXboxOne;
            StdPriceXbox360 = stdPriceXbox360;
            AddedOn = addedOn;
            PPlatform = platform;
            // this is useless unless we add special
            Level = rating >= 75 ? Level.Gold : (rating >= 65 ? Level.Silver : Level.Bronze);
        }

        public uint AssetId { get; }
        public uint BaseId { get; set; }
        public string CommonName { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public byte Rating { get; private set; }
        private uint StdPricePs3 { get; }
        private uint StdPricePs4 { get; }
        private uint StdPriceXboxOne { get; }
        private uint StdPriceXbox360 { get; }
        public string AddedOn { get; set; }
        public string PPlatform { get; set; }
        public Level Level { get; }

        public uint GetStdPrice(Platform platform)
        {
            switch (platform)
            {
                case Platform.Ps3:
                    return StdPricePs3;
                case Platform.Ps4:
                    return StdPricePs4;
                case Platform.XboxOne:
                    return StdPriceXboxOne;
                case Platform.Xbox360:
                    return StdPriceXbox360;
                case Platform.Pc:
                    return int.MaxValue;
                default:
                    return int.MaxValue;
            }
        }

        public static List<Player> GetAllPlayers()
        {
            try
            {
                return Database.GetAllPlayers();
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetAllPlayers();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetAllPlayers();
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetAllPlayers();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return new List<Player>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return new List<Player>();
            }
        }

        public static List<Player> GetAllCardWeightPlayers()
        {
            try
            {
                return Database.GetAllCardWeightPlayers();
            }
            catch (SshException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    Database.SshConnect();
                    return Database.GetAllCardWeightPlayers();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (MySqlException)
            {
                try
                {
                    Thread.Sleep(30*1000);
                    return Database.GetAllCardWeightPlayers();
                }
                catch (MySqlException)
                {
                    try
                    {
                        Thread.Sleep(30*1000);
                        return Database.GetAllCardWeightPlayers();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex.Message, ex.ToString());
                        return new List<Player>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message, ex.ToString());
                    return new List<Player>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return new List<Player>();
            }
        }
    }
}