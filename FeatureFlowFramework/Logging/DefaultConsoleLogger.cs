﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataStorage;
using System;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Logging
{
    public class DefaultConsoleLogger : IDataFlowSink
    {
        private readonly bool hasConsole = CheckHasConsole();
        private StringBuilder stringBuilder = new StringBuilder();

        public class Config : Configuration
        {
            public string format = ">>{0}: {1} | {2} | {3} | {4} | {9}<<\n";
            internal Loglevel logFileLoglevel = Loglevel.WARNING;
        }

        public Config config = new Config();

        public void Post<M>(in M message)
        {
            if(!hasConsole) return;

            config.TryUpdateFromStorage(true);

            if(message is LogMessage logMessage)
            {
                if(logMessage.level <= config.logFileLoglevel)
                {
                    string strMsg;
                    lock(stringBuilder)
                    {
                        strMsg = logMessage.PrintToStringBuilder(stringBuilder).ToString();
                        stringBuilder.Clear();
                    }
                    Console.WriteLine(strMsg);
                }
            }
            else
            {
                Console.WriteLine(message.ToString());
            }
        }

        public static bool CheckHasConsole()
        {
            try
            {
                var x = Console.WindowHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }
    }
}
