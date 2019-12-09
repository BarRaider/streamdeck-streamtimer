using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamTimer.Wrappers
{
    internal class PlaybackDevice
    {
        [JsonProperty(PropertyName = "name")]
        public string ProductName {get; set;}
    }
}
