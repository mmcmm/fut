using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace EvFutBot.Utilities
{
    public static class CookieUtil
    {
        public static readonly string Dir = "C:\\EvFutBotUpdater\\Cookies";

        public static void WriteCookiesToDisk(string file, CookieContainer c)
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            using (Stream stream = File.Create(file))
            {
                try
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(stream, c);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex.Message);
                }
            }
        }

        public static CookieContainer ReadCookiesFromDisk(string file)
        {
            try
            {
                using (Stream stream = File.Open(file, FileMode.Open))
                {
                    var formatter = new BinaryFormatter();
                    return (CookieContainer) formatter.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                return new CookieContainer();
            }
        }

        public static void DeleteCookieFromDisk(string file)
        {
            File.Delete(file);
        }
    }
}