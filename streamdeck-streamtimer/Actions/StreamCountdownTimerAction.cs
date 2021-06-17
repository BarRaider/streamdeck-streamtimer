using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamTimer.Backend;
using StreamTimer.Wrappers;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamTimer.Actions
{
    [PluginActionId("com.barraider.streamcountdowntimer")]

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: TheLifeOfKB
    // 300 Bits: Nachtmeister666
    // Icessassin - Tip: $20.02
    // onemousegaming - Tip: $3.50
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
                    TimerInterval = DEFAULT_TIMER_INTERVAL,
                    AlertColor = "#FF0000",
                    HourglassColor = "#000000",
                    StreamathonIncrement = String.Empty,
                    PlaybackDevice = String.Empty,
                    PlaybackDevices = null,
                    PlaySoundOnEndFile = String.Empty,
                    PauseImageFile = String.Empty,
                    HourglassTime = false,
                    HourglassImageMode = false,
                    AutoResetSeconds = DEFAULT_AUTO_RESET_SECONDS.ToString(),
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

            [FilenameProperty]
            [JsonProperty(PropertyName = "pauseImageFile")]
            public string PauseImageFile { get; set; }

            [JsonProperty(PropertyName = "hourglassTime")]
            public bool HourglassTime { get; set; }

            [JsonProperty(PropertyName = "hourglassImageMode")]
            public bool HourglassImageMode { get; set; }

            [JsonProperty(PropertyName = "autoResetSeconds")]
            public string AutoResetSeconds { get; set; }
        }

        #region Private members

        private const int RESET_COUNTER_KEYPRESS_LENGTH_MS = 600;
        private const int TOTAL_ALERT_STAGES = 4;
        private const int DEFAULT_AUTO_RESET_SECONDS = 0;
        private const string DEFAULT_TIMER_INTERVAL = "00:01:00";

        private readonly Timer tmrAlert = new Timer();
        private bool isAlerting = false;
        private int alertStage = 0;

        private readonly PluginSettings settings;
        private bool keyPressed = false;
        private DateTime keyPressStart;
        private readonly string timerId;
        private TimeSpan timerInterval;
        private TimeSpan streamathonIncrement = TimeSpan.Zero;
        private long highestTimerSeconds;
        private bool displayCurrentStatus = false;
        private Image pauseImage = null;
        private bool stopPlayback = false;
        private int autoResetSeconds = DEFAULT_AUTO_RESET_SECONDS;

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
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            timerId = Connection.ContextId;
            tmrAlert.Interval = 200;
            tmrAlert.Elapsed += TmrAlert_Elapsed;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            tmrAlert.Elapsed -= TmrAlert_Elapsed;
            tmrAlert.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool clearOnReset = settings.ClearFileOnReset;
            string countdownEndText = settings.CountdownEndText;
            bool playSound = settings.PlaySoundOnEnd;
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

            if (playSound != settings.PlaySoundOnEnd && settings.PlaySoundOnEnd)
            {
                PropagatePlaybackDevices();
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
                await ResetAlert();
                return;
            }

            if (settings.StreamathonMode && TimerManager.Instance.IsTimerEnabled(timerId))
            {
                // Increment the timer
                if (!TimerManager.Instance.IncrementTimer(timerId, streamathonIncrement))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"TimerManager IncrementTimer failed");
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
            long total;

            // Stream Deck calls this function every second, 
            // so this is the best place to determine if we need to reset (versus the internal timer which may be paused)
            CheckIfResetNeeded();

            if (isAlerting)
            {
                var endTime = TimerManager.Instance.GetTimerEndTime(timerId);
                if (endTime > DateTime.MinValue)
                {
                    long timeElapsed = (long)(DateTime.Now - endTime).TotalSeconds;
                    await ShowTimeOnKey(timeElapsed);

                    if (autoResetSeconds > 0 && timeElapsed > autoResetSeconds)
                    {
                        await ResetAlert();
                    }
                }
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
            else if (total > highestTimerSeconds) // Streamathon Mode, when INCREASING the time left. Needed to correctly show the hourglass
            {
                highestTimerSeconds = total;
            }

            if (!TimerManager.Instance.IsTimerEnabled(timerId) && pauseImage != null)
            {
                await Connection.SetImageAsync(pauseImage);
                await Connection.SetTitleAsync((string)null);
                return;
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
                if (settings.HourglassTime)
                {
                    await ShowTimeOnKey(total);
                }
            }
            else // Not Hourglass mode
            {
                await Connection.SetImageAsync((string)null);
                await ShowTimeOnKey(total);
            }
        }

        #endregion

        #region Private methods

        private async Task ShowTimeOnKey(long total)
        {
            long minutes, seconds, hours;
            string delimiter = settings.Multiline ? "\n" : ":";
            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            string hoursStr = (hours > 0) ? $"{hours:0}{delimiter}" : "";
            string secondsDelimiter = delimiter;
            if (!String.IsNullOrEmpty(hoursStr))
            {
                secondsDelimiter = "\n";
            }
            await Connection.SetTitleAsync($"{hoursStr}{minutes:00}{secondsDelimiter}{seconds:00}");
        }

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
            highestTimerSeconds = (long)timerInterval.TotalSeconds;
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

            if ((DateTime.Now - keyPressStart).TotalMilliseconds >= RESET_COUNTER_KEYPRESS_LENGTH_MS)
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
                string increment = settings.StreamathonIncrement;
                bool isNegative = false;
                if (increment[0] == '-')
                {
                    increment = increment.Substring(1);
                    isNegative = true;
                }
                if (!TimeSpan.TryParse(increment, out streamathonIncrement))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Streamathon Increment: {settings.StreamathonIncrement}");
                }
                else if (isNegative)
                {
                    streamathonIncrement = streamathonIncrement.Negate();
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
                    settings.TimerInterval = DEFAULT_TIMER_INTERVAL;
                    SaveSettings();
                }
                else
                {
                    highestTimerSeconds = (long)timerInterval.TotalSeconds;
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
            long totalSeconds = highestTimerSeconds;

            if (remainingSeconds <= 0)
            {
                return;
            }
            else if (remainingSeconds > totalSeconds)
            {
                totalSeconds = remainingSeconds + 1;
            }

            double remainingPercentage = (double)remainingSeconds / (double)totalSeconds;

            Bitmap img = Tools.GenerateGenericKeyImage(out Graphics graphics);
            int height = img.Height;
            int width = img.Width;
            int startHeight = height - (int)(height * remainingPercentage);

            // Background

            if (settings.HourglassImageMode && pauseImage != null)
            {
                // Draw image
                graphics.DrawImage(pauseImage, new Rectangle(0, 0, width, height));

                // Cover the top parts based on the time left
                graphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, width, startHeight);
            }
            else
            {
                var color = GetHourglassColor(ColorTranslator.FromHtml(settings.HourglassColor), remainingPercentage);
                var bgBrush = new SolidBrush(color);
                graphics.FillRectangle(bgBrush, 0, startHeight, width, height);
            }

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

                if (!Int32.TryParse(settings.AutoResetSeconds, out autoResetSeconds))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid AutoResetSeconds: {settings.AutoResetSeconds}");
                    settings.AutoResetSeconds = DEFAULT_AUTO_RESET_SECONDS.ToString();
                    autoResetSeconds = DEFAULT_AUTO_RESET_SECONDS;
                    SaveSettings();
                }

                SetStreamahtonIncrement();
                PrefetchImages();
            });
        }

        private void PrefetchImages()
        {
            if (pauseImage != null)
            {
                pauseImage.Dispose();
                pauseImage = null;
            }

            if (!String.IsNullOrEmpty(settings.PauseImageFile))
            {
                if (!File.Exists(settings.PauseImageFile))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"PauseImageFile does not exist {settings.PauseImageFile}");
                    return;
                }

                pauseImage = Image.FromFile(settings.PauseImageFile);
            }
        }

        private void PropagatePlaybackDevices()
        {
            settings.PlaybackDevices = new List<PlaybackDevice>();

            try
            {
                if (settings.PlaySoundOnEnd)
                {
                    settings.PlaybackDevices = AudioUtils.Common.GetAllPlaybackDevices(true).Select(d => new PlaybackDevice() { ProductName = d }).OrderBy(p => p.ProductName).ToList();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }

        private void PlaySoundOnEnd()
        {
            Task.Run(async () =>
            {
                if (!settings.PlaySoundOnEnd)
                {
                    return;
                }

                stopPlayback = false;
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
                await AudioUtils.Common.PlaySound(settings.PlaySoundOnEndFile, settings.PlaybackDevice);
            });
        }

        private void StopPlayback()
        {
            stopPlayback = true;
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            Logger.Instance.LogMessage(TracingLevel.INFO, "OnSendToPlugin called");
            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
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

        private void Connection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
            PropagatePlaybackDevices();
        }

        private async Task ResetAlert()
        {
            isAlerting = false;
            tmrAlert.Stop();
            StopPlayback();
            ResetTimer();
            await Connection.SetImageAsync((string)null);
        }

        #endregion
    }
}