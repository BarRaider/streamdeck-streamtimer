using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamTimer
{
    public class TimerStatus
    {
        public DateTime EndTime { get; set; }

        public bool IsEnabled { get; set; }

        public string Filename { get; set; }

        public string FileTitlePrefix { get; set; }

        public string FileCountdownEndText { get; set; }

        public bool ClearFileOnReset { get; set; }

        public int PausedTimeLeft { get; set; }

        public TimerStatus()
        {
            EndTime = DateTime.Now;
            IsEnabled = false;
            Filename = null;
            FileTitlePrefix = null;
            FileCountdownEndText = null;
            ClearFileOnReset = false;
            PausedTimeLeft = 0;
        }
    }
}
