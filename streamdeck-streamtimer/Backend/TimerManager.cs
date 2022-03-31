using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using StreamTimer.Wrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamTimer.Backend
{
    internal class TimerManager
    {
        #region Private members
        private static TimerManager instance = null;
        private static readonly object objLock = new object();

        private readonly Timer tmrTimerCounter;
        private Dictionary<string, TimerStatus> dicTimers = new Dictionary<string, TimerStatus>();
        private GlobalSettings global;

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

        public bool IsInitialized { get; private set; }

        private TimerManager()
        {
            IsInitialized = false;
            tmrTimerCounter = new Timer
            {
                Interval = 1000
            };
            tmrTimerCounter.Elapsed += TmrTimerCounter_Elapsed;
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                dicTimers = global.DicTimers;
            }

            if (!tmrTimerCounter.Enabled)
            {
                tmrTimerCounter.Start();
            }
            HandleElapsedTimers();

            IsInitialized = true;
        }

        #endregion

        #region Public Methods

        public void StartTimer(TimerSettings timerSettings)
        {
            if (!dicTimers.ContainsKey(timerSettings.TimerId))
            {
                dicTimers[timerSettings.TimerId] = new TimerStatus
                {
                    EndTime = DateTime.Now + timerSettings.CounterLength,
                    Filename = timerSettings.FileName,
                    FileTitlePrefix = timerSettings.FileTitlePrefix,
                    FileCountdownEndText = timerSettings.FileCountdownEndText,
                    ClearFileOnReset = timerSettings.ClearFileOnReset,
                    TimeFormat = timerSettings.TimeFormat
                };
            }
            else // We were paused, modify time left based on current time
            {
                dicTimers[timerSettings.TimerId].EndTime = DateTime.Now.AddSeconds(dicTimers[timerSettings.TimerId].PausedTimeLeft);
            }

            if (timerSettings.ResetOnStart || SecondsLeft(timerSettings.TimerId) <= 0)
            {
                ResetTimer(timerSettings);
            }
            dicTimers[timerSettings.TimerId].IsEnabled = true;
            SaveTimers();
        }

        public void StopTimer(string timerId)
        {
            if (dicTimers.ContainsKey(timerId))
            {
                CheckWriteTimerToFile(timerId);
                dicTimers[timerId].IsEnabled = false;
                dicTimers[timerId].PausedTimeLeft = Math.Max(SecondsLeft(timerId),0);
                SaveTimers();
            }
        }

        public void ResetTimer(TimerSettings timerSettings)
        {
            if (!dicTimers.ContainsKey(timerSettings.TimerId))
            {
                dicTimers[timerSettings.TimerId] = new TimerStatus();
            }
            dicTimers[timerSettings.TimerId].EndTime = DateTime.Now + timerSettings.CounterLength;
            dicTimers[timerSettings.TimerId].Filename = timerSettings.FileName;
            dicTimers[timerSettings.TimerId].FileTitlePrefix = timerSettings.FileTitlePrefix;
            dicTimers[timerSettings.TimerId].FileCountdownEndText = timerSettings.FileCountdownEndText;
            dicTimers[timerSettings.TimerId].ClearFileOnReset = timerSettings.ClearFileOnReset;
            dicTimers[timerSettings.TimerId].PausedTimeLeft = 0;
            dicTimers[timerSettings.TimerId].TimeFormat = timerSettings.TimeFormat;
            SaveTimers();

            if (timerSettings.ClearFileOnReset)
            {
                HelperUtils.WriteToFile(timerSettings.FileName, String.Empty);
            }
        }

        public long GetTimerTime(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return 0;
            }

            if (IsTimerEnabled(timerId))
            {
                return SecondsLeft(timerId);
            }
            else
            {
                return dicTimers[timerId].PausedTimeLeft;
            }           
        }

        public bool IsTimerEnabled(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return false;
            }
            return dicTimers[timerId].IsEnabled;
        }

        public bool IncrementTimer(string timerId, TimeSpan increment)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return false;
            }
            dicTimers[timerId].EndTime += increment;
            SaveTimers();
            return true;
        }

        public DateTime GetTimerEndTime(string timerId)
        {
            if (!dicTimers.ContainsKey(timerId))
            {
                return DateTime.MinValue;
            }
            return dicTimers[timerId].EndTime;
        }

        #endregion

        #region Private Methods

        private void TmrTimerCounter_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (string key in dicTimers.Keys)
            {
                CheckWriteTimerToFile(key);
            }
        }

        private void CheckWriteTimerToFile(string timerKey)
        {
            if (dicTimers[timerKey].IsEnabled)
            {
                WriteCounterToFile(timerKey);
            }
        }

        private void WriteCounterToFile(string timerKey)
        {
            long total;
            var counterData = dicTimers[timerKey];

            if (String.IsNullOrEmpty(counterData.Filename))
            {
                return;
            }
            
            total = SecondsLeft(timerKey);
            if (total <= 0 && !String.IsNullOrEmpty(counterData.FileCountdownEndText))
            {
                HelperUtils.WriteToFile(counterData.Filename, counterData.FileCountdownEndText.Replace(@"\n", "\n"));
                return;
            }
            else if (total <= 0 && counterData.ClearFileOnReset)
            {
                HelperUtils.WriteToFile(counterData.Filename, String.Empty);
                return;
            }
            else if (total < 0)
            {
                total = 0;
            }

            string output = HelperUtils.FormatTime(total, counterData.TimeFormat, false);
            HelperUtils.WriteToFile(counterData.Filename, $"{counterData.FileTitlePrefix?.Replace(@"\n", "\n")}{output}");
        }

        private long SecondsLeft(string counterKey)
        {
            if (!dicTimers.ContainsKey(counterKey))
            {
                return -1;
            }

            return (long)(dicTimers[counterKey].EndTime - DateTime.Now).TotalSeconds;
        }

        private void SaveTimers()
        {
            if (global == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"SaveTimers - global is null, creating new object");
                global = new GlobalSettings();
            }
            global.DicTimers = dicTimers;
            GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
        }

        private void HandleElapsedTimers()
        {
            foreach (string key in dicTimers.Keys)
            {
                if (dicTimers[key].IsEnabled)
                {
                    if (SecondsLeft(key) < 0)
                    {
                        dicTimers[key].IsEnabled = false;
                        dicTimers[key].PausedTimeLeft = 0;
                    }
                }
            }
        }

        #endregion
    }
}
