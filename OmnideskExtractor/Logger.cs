using System;
using NLog;

namespace Extractor
{
    class NLogger
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void InfoLog(string message)
        {
            try
            {
                Logger.Info(message);
            }
            catch (Exception e)
            {
                Logger.Error("Fail to write InfoLog: " + e);
            }
        }

        public static void ErrorLog(string message)
        {
            try
            {
                Logger.Error(message);
            }
            catch
            {
                //throw;
            }
        }
    }
}
