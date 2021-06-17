using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamTimer.Backend;
using StreamTimer.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StreamTimer.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Sm0ozle - Tip: $3.65
    // Subscriber: Grumtastic
    //---------------------------------------------------   
    [PluginActionId("com.barraider.streamcountdowntimer.focustimer")]
    public class FocusTimerAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ResumeOnClick = true,
                    Multiline = false,
                    ClearFileOnReset = false,
                    PlaySoundOnEnd = false,
                    TimerFileName = String.Empty,
                    FilePrefix = String.Empty,
                    CountdownEndText = String.Empty,
                    WorkInterval = DEFAULT_WORK_INTERVAL,
                    BreakInterval = DEFAULT_BREAK_INTERVAL,
                    LongBreakInterval = DEFAULT_LONG_BREAK_INTERVAL,
                    RepeatAmount = DEFAULT_REPEAT_AMOUNT.ToString(),
                    AlertColor = "#FF0000",
                    PlaybackDevice = String.Empty,
                    PlaybackDevices = null,
                    PlaySoundOnEndFile = String.Empty,
                    HourglassTime = true,
                    WorkImageFile = null,
                    BreakImageFile = null
                };

                return instance;
            }

            [JsonProperty(PropertyName = "resumeOnClick")]
            public bool ResumeOnClick { get; set; }

            [JsonProperty(PropertyName = "multiline")]
            public bool Multiline { get; set; }

            [JsonProperty(PropertyName = "workInterval")]
            public string WorkInterval { get; set; }

            [JsonProperty(PropertyName = "breakInterval")]
            public string BreakInterval { get; set; }

            [JsonProperty(PropertyName = "longBreakInterval")]
            public string LongBreakInterval { get; set; }

            [JsonProperty(PropertyName = "repeatAmount")]
            public string RepeatAmount { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "timerFileName")]
            public string TimerFileName { get; set; }

            [JsonProperty(PropertyName = "filePrefix")]
            public string FilePrefix { get; set; }

            [JsonProperty(PropertyName = "alertColor")]
            public string AlertColor { get; set; }

            [JsonProperty(PropertyName = "countdownEndText")]
            public string CountdownEndText { get; set; }

            [JsonProperty(PropertyName = "clearFileOnReset")]
            public bool ClearFileOnReset { get; set; }

            [JsonProperty(PropertyName = "playSoundOnEnd")]
            public bool PlaySoundOnEnd { get; set; }

            [JsonProperty(PropertyName = "playbackDevices")]
            public List<PlaybackDevice> PlaybackDevices { get; set; }

            [JsonProperty(PropertyName = "playbackDevice")]
            public string PlaybackDevice { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "playSoundOnEndFile")]
            public string PlaySoundOnEndFile { get; set; }

            [JsonProperty(PropertyName = "hourglassTime")]
            public bool HourglassTime { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "workImageFile")]
            public string WorkImageFile { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "breakImageFile")]
            public string BreakImageFile { get; set; }
        }

        #region Private members

        private const int RESET_COUNTER_KEYPRESS_LENGTH_MS = 600;
        private const int TOTAL_ALERT_STAGES = 4;
        private const int DEFAULT_REPEAT_AMOUNT = 4;
        private const string DEFAULT_WORK_INTERVAL = "00:25:00";
        private const string DEFAULT_BREAK_INTERVAL = "00:05:00";
        private const string DEFAULT_LONG_BREAK_INTERVAL = "00:15:00";
        private const string DEFAULT_WORK_IMAGE = @"images\potato.png";
        private const string DEFAULT_BREAK_IMAGE = @"images\break.png";

        private readonly Timer tmrAlert = new Timer();
        private bool isAlerting = false;
        private int alertStage = 0;

        private readonly PluginSettings settings;
        private bool keyPressed = false;
        private DateTime keyPressStart;
        private readonly string timerId;
        private TimeSpan workInterval;
        private TimeSpan breakInterval;
        private TimeSpan longBreakInterval;
        private TimeSpan timerInterval;
        private int repeatAmount = DEFAULT_REPEAT_AMOUNT;
        private bool displayCurrentStatus = false;
        private Image keyImage = null;
        private bool stopPlayback = false;
        private FocusState currentMode = FocusState.WORK;
        private int cycleNumber = 0;

        #endregion

        #region PluginBase Methods

        public FocusTimerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            displayCurrentStatus = true;

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (isAlerting)
            {
                isAlerting = false;
                tmrAlert.Stop();
                StopPlayback();
                SetFocusMode();
                PrefetchImages();
                ResumeTimer();
                await Connection.SetImageAsync((string)null);
                return;
            }

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

            if (!TimerManager.Instance.IsTimerEnabled(timerId) && keyImage != null)
            {
                await Connection.SetImageAsync(keyImage);
                await Connection.SetTitleAsync((string)null);
                return;
            }

            // Handle hourglass mode
            if (keyImage != null)
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
                currentMode = FocusState.WORK;
                SetTimerInterval();
                PrefetchImages();
            }
        }

        private void PauseTimer()
        {
            TimerManager.Instance.StopTimer(timerId);
        }

        private void SetTimerInterval()
        {
            if (!Int32.TryParse(settings.RepeatAmount, out repeatAmount))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Repeat Amount: {settings.BreakInterval}");
                settings.RepeatAmount = DEFAULT_REPEAT_AMOUNT.ToString();
                SaveSettings();
            }

            if (!TimeSpan.TryParse(settings.BreakInterval, out breakInterval))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Break Interval: {settings.BreakInterval}");
                settings.BreakInterval = DEFAULT_BREAK_INTERVAL;
                SaveSettings();
            }

            if (!TimeSpan.TryParse(settings.LongBreakInterval, out longBreakInterval))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Long Break Interval: {settings.LongBreakInterval}");
                settings.LongBreakInterval = DEFAULT_LONG_BREAK_INTERVAL;
                SaveSettings();
            }

            if (!TimeSpan.TryParse(settings.WorkInterval, out workInterval))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid Timer Interval: {settings.WorkInterval}");
                settings.WorkInterval = DEFAULT_WORK_INTERVAL;
                SaveSettings();
                return;
            }

            switch (currentMode)
            {
                case FocusState.WORK:
                    timerInterval = workInterval;
                    break;
                case FocusState.BREAK:
                    timerInterval = breakInterval;
                    break;
                case FocusState.LONG_BREAK:
                    timerInterval = longBreakInterval;
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetTimerInterval invalid mode {currentMode}");
                    timerInterval = workInterval;
                    break;
            }

            if (!TimerManager.Instance.IsTimerEnabled(timerId))
            {
                ResetTimer();
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

            // Background
            if (keyImage != null)
            {
                // Draw image
                graphics.DrawImage(keyImage, new Rectangle(0, 0, width, height));

                // Cover the top parts based on the time left
                graphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, width, startHeight);
            }
            else
            {
                var color = Color.Black;
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
                PrefetchImages();
            });
        }

        private void PrefetchImages()
        {
            if (keyImage != null)
            {
                keyImage.Dispose();
                keyImage = null;
            }

            if (currentMode == FocusState.WORK)
            {
                TryLoadCustomImage(settings.WorkImageFile);
                if (keyImage == null)
                {
                    keyImage = Image.FromFile(DEFAULT_WORK_IMAGE);
                }
            }
            else
            {
                TryLoadCustomImage(settings.BreakImageFile);
                if (keyImage == null)
                {
                    keyImage = Image.FromFile(DEFAULT_BREAK_IMAGE);
                }
            }
        }

        private void TryLoadCustomImage(string fileName)
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                if (!File.Exists(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"CustomImageFile does not exist {fileName}");

                }
                else
                {
                    keyImage = Image.FromFile(fileName);
                }
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

        private void SetFocusMode()
        {
            switch (currentMode)
            {
                case FocusState.BREAK:
                case FocusState.LONG_BREAK:
                    currentMode = FocusState.WORK;
                    break;
                case FocusState.WORK:
                    currentMode = FocusState.BREAK;
                    cycleNumber = (cycleNumber + 1) % repeatAmount;
                    if (cycleNumber == 0)
                    {
                        currentMode = FocusState.LONG_BREAK;
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid Focus Mode {currentMode}");
                    break;
            }
            SetTimerInterval();
        }

        #endregion
    }
}