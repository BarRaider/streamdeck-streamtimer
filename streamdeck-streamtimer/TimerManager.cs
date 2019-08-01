using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamTimer
{
    internal class TimerManager
    {
        #region Private members
        private static TimerManager instance = null;
        private static readonly object objLock = new object();

        private readonly Dictionary<string, TimerStatus> dicTimers = new Dictionary<string, TimerStatus>();
        private readonly Timer tmrTimerCounter;

        #endregion

        #region Constructors

        public static TimerManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new TimerManager();
                    }
                    return instance;
                }
            }
        }

        private TimerManager()
        {
            tmrTimerCounter = new Timer
            {
                Interval = 1000
            };
            tmrTimerCounter.Elapsed += TmrTimerCounter_Elapsed;
            tmrTimerCounter.Start();
        }

        #endregion

        #region Public Methods

        public void StartTimer(string timerId, bool resetOnStart, TimeSpan counterLength, string fileName, string fileTitlePrefix)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                dicTimers[timerId] = new TimerStatus
                {
                    Counter = (int)counterLength.TotalSeconds,
                    Filename = fileName,
                    FileTitlePrefix = fileTitlePrefix
                };
            }

            if (resetOnStart || dicTimers[timerId].Counter <= 0)
            {
                ResetTimer(timerId, counterLength, fileName, fileTitlePrefix);
            }
            dicTimers[timerId].IsEnabled = true;
        }

        public void StopTimer(string timerId)
        {
            if (dicTimers.ContainsKey(timerId))
            {
                dicTimers[timerId].IsEnabled = false;
            }
        }

        public void ResetTimer(string timerId, TimeSpan counterLength, string fileName, string fileTitlePrefix)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                dicTimers[timerId] = new TimerStatus();
            }
            dicTimers[timerId].Counter = (int)counterLength.TotalSeconds;
            dicTimers[timerId].Filename = fileName;
            dicTimers[timerId].FileTitlePrefix = fileTitlePrefix;
        }

        public long GetTimerTime(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return 0;
            }
            return dicTimers[timerId].Counter;
        }

        public bool IsTimerEnabled(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return false;
            }
            return dicTimers[timerId].IsEnabled;
        }

        #endregion

        #region Private Methods

        private void TmrTimerCounter_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (string key in dicTimers.Keys)
            {
                if (dicTimers[key].IsEnabled)
                {
                    dicTimers[key].Counter--;

                    if (dicTimers[key].Counter < 0)
                    {
                        dicTimers[key].Counter = 0;
                    }

                    WriteCounterToFile(key);
                }
            }
        }

        private void SaveTimerToFile(string fileName, string text)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(fileName))
                {

                    File.WriteAllText(fileName, text);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error Saving value: {text} to counter file: {fileName} : {ex}");
            }
        }

        private void WriteCounterToFile(string counterKey)
        {
            long total, minutes, seconds, hours;
            var counterData = dicTimers[counterKey];

            if (String.IsNullOrEmpty(counterData.Filename))
            {
                return;
            }

            
            total = counterData.Counter;
            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            string hoursStr = (hours > 0) ? $"{hours.ToString("00")}:" : "";
            SaveTimerToFile(counterData.Filename, $"{counterData.FileTitlePrefix}{hoursStr}{minutes.ToString("00")}:{seconds.ToString("00")}");
        }

        #endregion
    }
}
