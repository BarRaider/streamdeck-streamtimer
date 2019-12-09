using BarRaider.SdTools;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamTimer.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamTimer
{
    [PluginActionId("com.barraider.streamcountdowntimer")]

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: TheLifeOfKB
    // 300 Bits: Nachtmeister666
    // Icessassin - Tip: $20.02
    //---------------------------------------------------
    public class StreamCountdownTimerAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ResumeOnClick = false,
                    Multiline = false,
                    HourglassMode = false,
                    ClearFileOnReset = false,
                    StreamathonMode = false,
                    PlaySoundOnEnd = false,
                    TimerFileName = String.Empty,
                    FilePrefix = String.Empty,
                    CountdownEndText = String.Empty,
                    TimerInterval = "00:01:00",
                    AlertColor = "#FF0000",
                    HourglassColor = "#000000",
                    StreamathonIncrement = String.Empty,
                    PlaybackDevice = String.Empty,
                    PlaybackDevices = null,
                    PlaySoundOnEndFile = String.Empty
                };

                return instance;
            }

            [JsonProperty(PropertyName = "resumeOnClick")]
            public bool ResumeOnClick { get; set; }

            [JsonProperty(PropertyName = "multiline")]
            public bool Multiline { get; set; }

            [JsonProperty(PropertyName = "timerInterval")]
            public string TimerInterval { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "timerFileName")]
            public string TimerFileName { get; set; }

            [JsonProperty(PropertyName = "filePrefix")]
            public string FilePrefix { get; set; }

            [JsonProperty(PropertyName = "alertColor")]
            public string AlertColor { get; set; }

            [JsonProperty(PropertyName = "hourglassMode")]
            public bool HourglassMode { get; set; }

            [JsonProperty(PropertyName = "hourglassColor")]
            public string HourglassColor { get; set; }

            [JsonProperty(PropertyName = "countdownEndText")]
            public string CountdownEndText { get; set; }

            [JsonProperty(PropertyName = "clearFileOnReset")]
            public bool ClearFileOnReset { get; set; }

            [JsonProperty(PropertyName = "streamathonMode")]
            public bool StreamathonMode { get; set; }

            [JsonProperty(PropertyName = "streamathonIncrement")]
            public string StreamathonIncrement { get; set; }

            [JsonProperty(PropertyName = "playSoundOnEnd")]
            public bool PlaySoundOnEnd { get; set; }

            [JsonProperty(PropertyName = "playbackDevices")]
            public List<PlaybackDevice> PlaybackDevices { get; set; }

            [JsonProperty(PropertyName = "playbackDevice")]
            public string PlaybackDevice { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "playSoundOnEndFile")]
            public string PlaySoundOnEndFile { get; set; }

        }

        #region Private members

        private const int RESET_COUNTER_KEYPRESS_LENGTH = 1;
        private const int TOTAL_ALERT_STAGES = 4;

        private readonly Timer tmrAlert = new Timer();
        private bool isAlerting = false;
        private int alertStage = 0;

        private readonly PluginSettings settings;
        private bool keyPressed = false;
        private DateTime keyPressStart;
        private readonly string timerId;
        private TimeSpan timerInterval;
        private TimeSpan streamathonIncrement;
        private bool displayCurrentStatus = false;

        #endregion

        #region PluginBase Methods

        public StreamCountdownTimerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                Connection.SetSettingsAsync(JObject.FromObject(settings));
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            Connection.StreamDeckConnection.OnSendToPlugin += StreamDeckConnection_OnSendToPlugin;
            timerId = Connection.ContextId;
            tmrAlert.Interval = 200;
            tmrAlert.Elapsed += TmrAlert_Elapsed;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.StreamDeckConnection.OnSendToPlugin -= StreamDeckConnection_OnSendToPlugin;
            tmrAlert.Elapsed -= TmrAlert_Elapsed;
            tmrAlert.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool clearOnReset = settings.ClearFileOnReset;
            string countdownEndText = settings.CountdownEndText;
            // New in StreamDeck-Tools v2.0:
            Tools.AutoPopulateSettings(settings, payload.Settings);

            if (clearOnReset != settings.ClearFileOnReset)
            {
                settings.CountdownEndText = String.Empty;
            }
            else if (countdownEndText != settings.CountdownEndText)
            {
                settings.ClearFileOnReset = false;
            }
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public async override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            keyPressStart = DateTime.Now;
            keyPressed = true;

            if (settings.HourglassMode)
            {
                displayCurrentStatus = true;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (isAlerting)
            {
                isAlerting = false;
                tmrAlert.Stop();
                ResetTimer();
                await Connection.SetImageAsync((string)null);
                return;
            }

            if (settings.StreamathonMode && TimerManager.Instance.IsTimerEnabled(timerId))
            {
                // Increment the timer
                if (streamathonIncrement.TotalSeconds > 0)
                {
                    if (!TimerManager.Instance.IncrementTimer(timerId, streamathonIncrement))
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"TimerManager IncrementTimer failed");
                        await Connection.ShowAlert();
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Streamathon mode - Invalid Increment {settings.StreamathonIncrement}");
                    await Connection.ShowAlert();
                }
            }
            else // Either not in streamathon mode or timer is disabled
            {
                if (TimerManager.Instance.IsTimerEnabled(timerId))
                {
                    PauseTimer();
                }
                else
                {
                    if (!settings.ResumeOnClick)
                    {
                        ResetTimer();
                    }

                    ResumeTimer();
                }
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            keyPressed = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
        }

        public async override void OnTick()
        {
            long total, minutes, seconds, hours;
            string delimiter = settings.Multiline ? "\n" : ":";

            // Stream Deck calls this function every second, 
            // so this is the best place to determine if we need to reset (versus the internal timer which may be paused)
            CheckIfResetNeeded();

            if (isAlerting)
            {
                return;
            }

            // Handle alerting
            total = TimerManager.Instance.GetTimerTime(timerId);
            if (total <= 0 && !TimerManager.Instance.IsTimerEnabled(timerId)) // Time passed before 
            {
                total = (int)timerInterval.TotalSeconds;
            }
            else if (total <= 0 && !tmrAlert.Enabled) // Timer running, need to alert
            {
                total = 0;
                isAlerting = true;
                tmrAlert.Start();
                TimerManager.Instance.StopTimer(timerId);
                PlaySoundOnEnd();
            }

            // Handle hourglass mode
            if (settings.HourglassMode)
            {
                await DisplayHourglass(total);
                if (displayCurrentStatus)
                {
                    displayCurrentStatus = false;
                    await Connection.SetTitleAsync($"{(TimerManager.Instance.IsTimerEnabled(timerId) ? "▶️" : "||")}");
                }
                return;
            }

            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            await Connection.SetImageAsync((string)null);
            string hoursStr = (hours > 0) ? $"{hours.ToString("0")}{delimiter}" : "";
            string secondsDelimiter = delimiter;
            if (!String.IsNullOrEmpty(hoursStr))
            {
                secondsDelimiter = "\n";
            }
            await Connection.SetTitleAsync($"{hoursStr}{minutes.ToString("00")}{secondsDelimiter}{seconds.ToString("00")}");
        }

        #endregion

        #region Private methods

        private void ResetTimer()
        {
            TimerManager.Instance.ResetTimer(new TimerSettings()
            {
                TimerId = timerId,
                CounterLength = timerInterval,
                FileName = settings.TimerFileName,
                FileTitlePrefix = settings.FilePrefix,
                ResetOnStart = !settings.ResumeOnClick,
                FileCountdownEndText = settings.CountdownEndText,
                ClearFileOnReset = settings.ClearFileOnReset
            });
        }

        private void ResumeTimer()
        {
            TimerManager.Instance.StartTimer(new TimerSettings()
            {
                TimerId = timerId,
                CounterLength = timerInterval,
                FileName = settings.TimerFileName,
                FileTitlePrefix = settings.FilePrefix,
                ResetOnStart = !settings.ResumeOnClick,
                FileCountdownEndText = settings.CountdownEndText,
                ClearFileOnReset = settings.ClearFileOnReset
            });
        }

        private void CheckIfResetNeeded()
        {
            if (!keyPressed)
            {
                return;
            }

            if ((DateTime.Now - keyPressStart).TotalSeconds > RESET_COUNTER_KEYPRESS_LENGTH)
            {
                PauseTimer();
                ResetTimer();
            }
        }

        private void PauseTimer()
        {
            TimerManager.Instance.StopTimer(timerId);
        }

        private void SetStreamahtonIncrement()
        {
            streamathonIncrement = TimeSpan.Zero;
            if (settings.StreamathonMode && !String.IsNullOrEmpty(settings.StreamathonIncrement))
            {
                if (!TimeSpan.TryParse(settings.StreamathonIncrement, out streamathonIncrement))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Streamathon Increment: {settings.StreamathonIncrement}");
                }
            }
        }

        private void SetTimerInterval()
        {
            timerInterval = TimeSpan.Zero;
            if (!String.IsNullOrEmpty(settings.TimerInterval))
            {
                if (!TimeSpan.TryParse(settings.TimerInterval, out timerInterval))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Timer Interval: {settings.TimerInterval}");
                }
                else
                {
                    if (!TimerManager.Instance.IsTimerEnabled(timerId))
                    {
                        ResetTimer();
                    }
                }
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private Color GenerateStageColor(string initialColor, int stage, int totalAmountOfStages)
        {
            Color color = ColorTranslator.FromHtml(initialColor);
            int a = color.A;
            double r = color.R;
            double g = color.G;
            double b = color.B;

            // Try and increase the color in the last stage;
            if (stage == totalAmountOfStages - 1)
            {
                stage = 1;
            }

            for (int idx = 0; idx < stage; idx++)
            {
                r /= 2;
                g /= 2;
                b /= 2;
            }

            return Color.FromArgb(a, (int)r, (int)g, (int)b);
        }

        private void TmrAlert_Elapsed(object sender, ElapsedEventArgs e)
        {
            Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            int height = img.Height;
            int width = img.Width;

            // Background
            var bgBrush = new SolidBrush(GenerateStageColor(settings.AlertColor, alertStage, TOTAL_ALERT_STAGES));
            graphics.FillRectangle(bgBrush, 0, 0, width, height);
            Connection.SetImageAsync(img);

            alertStage = (alertStage + 1) % TOTAL_ALERT_STAGES;
            graphics.Dispose();
        }

        private Color GetHourglassColor(Color initialColor, double remainingPercentage)
        {
            if (initialColor.R != 0 || initialColor.G != 0 || initialColor.B != 0)
            {
                return initialColor;
            }

            if (remainingPercentage > 0.5)
            {
                return Color.Green;
            }
            else if (remainingPercentage > 0.20)
            {
                return Color.Yellow;
            }
            else
            {
                return Color.Red;
            }
        }

        private async Task DisplayHourglass(long remainingSeconds)
        {
            long totalSeconds = (long)timerInterval.TotalSeconds;

            if (remainingSeconds <= 0)
            {
                return;
            }

            double remainingPercentage = (double)remainingSeconds / (double)totalSeconds;

            Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            int height = img.Height;
            int width = img.Width;
            int startHeight = height - (int)(height * remainingPercentage);

            var color = GetHourglassColor(ColorTranslator.FromHtml(settings.HourglassColor), remainingPercentage);

            // Background
            var bgBrush = new SolidBrush(color);
            graphics.FillRectangle(bgBrush, 0, startHeight, width, height);
            await Connection.SetTitleAsync((string)null);
            await Connection.SetImageAsync(img);
            graphics.Dispose();
        }

        private void InitializeSettings()
        {
            Task.Run(() =>
            {
                int retries = 60;
                while (!TimerManager.Instance.IsInitialized && retries > 0)
                {
                    retries--;
                    System.Threading.Thread.Sleep(1000);
                }
                SetTimerInterval();
                SetStreamahtonIncrement();
                PropagatePlaybackDevices();
            });
        }

        private void PropagatePlaybackDevices()
        {
            settings.PlaybackDevices = new List<PlaybackDevice>();

            try
            {
                if (settings.PlaySoundOnEnd)
                {
                    for (int idx = -1; idx < WaveOut.DeviceCount; idx++)
                    {
                        var currDevice = WaveOut.GetCapabilities(idx);
                        settings.PlaybackDevices.Add(new PlaybackDevice() { ProductName = currDevice.ProductName });
                    }

                    settings.PlaybackDevices = settings.PlaybackDevices.OrderBy(p => p.ProductName).ToList();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }

        private Task PlaySoundOnEnd()
        {
            return Task.Run(() =>
            {
                // Q98NF-KR5LZ-DWBAB
                if (!settings.PlaySoundOnEnd)
                {
                    return;
                }

                if (String.IsNullOrEmpty(settings.PlaySoundOnEndFile) || string.IsNullOrEmpty(settings.PlaybackDevice))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnEnd called but File or Playback device are empty. File: {settings.PlaySoundOnEndFile} Device: {settings.PlaybackDevice}");
                    return;
                }

                if (!File.Exists(settings.PlaySoundOnEndFile))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnEnd called but file does not exist: {settings.PlaySoundOnEndFile}");
                    return;
                }

                Logger.Instance.LogMessage(TracingLevel.INFO, $"PlaySoundOnEnd called. Playing {settings.PlaySoundOnEndFile} on device: {settings.PlaybackDevice}");
                var deviceNumber = GetPlaybackDeviceFromDeviceName(settings.PlaybackDevice); using (var audioFile = new AudioFileReader(settings.PlaySoundOnEndFile))
                {
                    using (var outputDevice = new WaveOutEvent())
                    {
                        outputDevice.DeviceNumber = deviceNumber;
                        outputDevice.Init(audioFile);
                        outputDevice.Play();
                        while (outputDevice.PlaybackState == PlaybackState.Playing)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                }
            });
        }

        private int GetPlaybackDeviceFromDeviceName(string deviceName)
        {
            for (int idx = -1; idx < WaveOut.DeviceCount; idx++)
            {
                var currDevice = WaveOut.GetCapabilities(idx);
                if (deviceName == currDevice.ProductName)
                {
                    return idx;
                }
            }
            return -1;
        }

        private void StreamDeckConnection_OnSendToPlugin(object sender, streamdeck_client_csharp.StreamDeckEventReceivedEventArgs<streamdeck_client_csharp.Events.SendToPluginEvent> e)
        {
            var payload = e.Event.Payload;
            if (Connection.ContextId != e.Event.Context)
            {
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, "OnSendToPlugin called");
            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLower())
                {
                    case "loadsavepicker":
                        string propertyName = (string)payload["property_name"];
                        string pickerTitle = (string)payload["picker_title"];
                        string pickerFilter = (string)payload["picker_filter"];
                        string fileName = PickersUtil.Pickers.SaveFilePicker(pickerTitle, null, pickerFilter);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, propertyName, fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                        }
                        break;
                }
            }
        }

        #endregion
    }
}