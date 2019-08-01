﻿using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                    TimerFileName = String.Empty,
                    FilePrefix = String.Empty,
                    TimerInterval = "00:01:00",
                    AlertColor = "#FF0000",
                    HourglassColor = "#000000"
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
        private readonly StreamDeckDeviceType deviceType;

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
            timerId = Connection.ContextId;
            tmrAlert.Interval = 200;
            tmrAlert.Elapsed += TmrAlert_Elapsed;
            deviceType = Connection.DeviceInfo().Type;
            SetTimerInterval();
        }

        public override void Dispose()
        {
            tmrAlert.Elapsed -= TmrAlert_Elapsed;
            tmrAlert.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            // New in StreamDeck-Tools v2.0:
            Tools.AutoPopulateSettings(settings, payload.Settings);

            SetTimerInterval();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public async override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            keyPressStart = DateTime.Now;
            keyPressed = true;

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (isAlerting)
            {
                isAlerting = false;
                tmrAlert.Stop();
                ResetTimer();
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
            if (total == 0 && !tmrAlert.Enabled)
            {
                isAlerting = true;
                tmrAlert.Start();
                TimerManager.Instance.StopTimer(timerId);
            }

            // Handle hourglass mode
            if (settings.HourglassMode)
            {
                await DisplayHourglass();
                return;
            }

            minutes = total / 60;
            seconds = total % 60;
            hours = minutes / 60;
            minutes %= 60;

            await Connection.SetImageAsync((string)null);
            await Connection.SetTitleAsync($"{hours.ToString("00")}{delimiter}{minutes.ToString("00")}\n{seconds.ToString("00")}");
        }

        #endregion

        #region Private methods

        private void ResetTimer()
        {
            TimerManager.Instance.ResetTimer(timerId, timerInterval, settings.TimerFileName, settings.FilePrefix);
        }

        private void ResumeTimer()
        {
            bool reset = !settings.ResumeOnClick;
            TimerManager.Instance.StartTimer(timerId, reset, timerInterval, settings.TimerFileName, settings.FilePrefix);
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
            Bitmap img = Tools.GenerateKeyImage(deviceType, out Graphics graphics);
            int height = Tools.GetKeyDefaultHeight(deviceType);
            int width = Tools.GetKeyDefaultWidth(deviceType);

            // Background
            var bgBrush = new SolidBrush(GenerateStageColor(settings.AlertColor, alertStage, TOTAL_ALERT_STAGES));
            graphics.FillRectangle(bgBrush, 0, 0, width, height);

            /*
            var font = new Font("Verdana", 50, FontStyle.Bold);
            var fgBrush = Brushes.White;
            SizeF stringSize = graphics.MeasureString(message, font);
            float stringPos = 0;
            float stringHeight = 54;
            if (stringSize.Width < width)
            {
                stringPos = Math.Abs((width - stringSize.Width)) / 2;
                stringHeight = Math.Abs((height - stringSize.Height)) / 2;
            }
            graphics.DrawString(message, font, fgBrush, new PointF(stringPos, stringHeight));
            */
            Connection.SetImageAsync(img);

            alertStage = (alertStage + 1) % TOTAL_ALERT_STAGES;
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

        private async Task DisplayHourglass()
        {
            long totalSeconds = (long) timerInterval.TotalSeconds;
            long remainingSeconds = TimerManager.Instance.GetTimerTime(timerId);

            if (remainingSeconds <= 0)
            {
                return;
            }

            double remainingPercentage = (double)remainingSeconds / (double)totalSeconds;

            Bitmap img = Tools.GenerateKeyImage(deviceType, out Graphics graphics);
            int height = Tools.GetKeyDefaultHeight(deviceType);
            int width = Tools.GetKeyDefaultWidth(deviceType);
            int startHeight = height - (int)(height * remainingPercentage);

            var color = GetHourglassColor(ColorTranslator.FromHtml(settings.HourglassColor), remainingPercentage);

            // Background
            var bgBrush = new SolidBrush(color);
            graphics.FillRectangle(bgBrush, 0, startHeight , width, height);
            await Connection.SetTitleAsync((string)null);
            await Connection.SetImageAsync(img);
        }

        #endregion
    }
}
;