using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamTimer
{
    public class TimerStatus
    {
        public long Counter { get; set; }

        public bool IsEnabled { get; set; }

        public TimerStatus()
        {
            Counter = 0;
            IsEnabled = false;
        }
    }
}
