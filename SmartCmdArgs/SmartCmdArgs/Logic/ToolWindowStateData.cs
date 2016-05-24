using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace SmartCmdArgs.Logic
{
    public class ToolWindowStateSolutionData : Dictionary<string, ToolWindowStateProjectData>
    {}

    public class ToolWindowStateProjectData
    {
        public List<ListEntryData> DataCollection = new List<ListEntryData>();

        public class ListEntryData
        {
            public Guid Id = Guid.NewGuid();
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Command = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Project = null; // this one is useles
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool Enabled = false;

            [OnError]
            public void OnError(StreamingContext context, ErrorContext errorContext)
            {
                if (errorContext?.Member?.ToString() == nameof(Id))
                {
                    errorContext.Handled = true;
                }
            }
        }
    }

}
