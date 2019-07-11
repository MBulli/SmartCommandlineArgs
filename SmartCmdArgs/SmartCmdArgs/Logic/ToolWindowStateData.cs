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
    public class ToolWindowStateSolutionData
    {
        public int FileVersion = 2;
        public bool ShowAllProjects;
        public HashSet<Guid> CheckedArguments = new HashSet<Guid>();
        public HashSet<Guid> ExpandedContainer = new HashSet<Guid>();
        public Dictionary<Guid, ToolWindowStateProjectData> ProjectArguments = new Dictionary<Guid, ToolWindowStateProjectData>();
    }

    public class ToolWindowStateProjectData : ListEntryData
    {
        public int FileVersion = 2;

        public ToolWindowStateProjectData()
        {
            Items = new List<ListEntryData>();
        }
    }
    
    public class ListEntryData
    {
        public Guid Id = Guid.NewGuid();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Command = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProjectConfig = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LaunchProfile = null;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ExclusiveMode = false;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ListEntryData> Items = null;

        [JsonIgnore]
        public bool Enabled = false;
        [JsonIgnore]
        public bool Expanded = false;

        [JsonIgnore]
        public IEnumerable<ListEntryData> AllArguments => Items.Where(item => item.Items == null)
            .Concat(Items.Where(item => item.Items != null).SelectMany(container => container.AllArguments));

        [JsonIgnore]
        public IEnumerable<ListEntryData> AllContainer => Items.Where(item => item.Items != null)
            .Concat(Items.Where(item => item.Items != null).SelectMany(container => container.AllContainer));

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
