using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamTimer
{
    public class GlobalSettings
    {
        [JsonProperty(PropertyName = "timers")]
        public Dictionary<string, TimerStatus> DicTimers { get; set; }
    }
}
