using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public class SettingsJson
    {
        public bool VcsSupportEnabled = true;
        public bool UseSolutionDir = false;
        public bool MacroEvaluationEnabled = true;

        public bool IsDefault()
        {
            return VcsSupportEnabled == true
                && UseSolutionDir == false
                && MacroEvaluationEnabled == true;
        }
    }

    public class SuoDataJson
    {
        public int FileVersion = 2;

        public bool ShowAllProjects;
        public HashSet<Guid> SelectedItems = new HashSet<Guid>();
        public HashSet<Guid> CheckedArguments = new HashSet<Guid>();
        public HashSet<Guid> ExpandedContainer = new HashSet<Guid>();
        
        public Dictionary<Guid, ProjectDataJson> ProjectArguments = new Dictionary<Guid, ProjectDataJson>();
    }

    public class SolutionDataJson
    {
        public int FileVersion = 2;

        public List<ProjectDataJson> ProjectArguments = new List<ProjectDataJson>();
    }

    public class ProjectDataJson : CmdArgumentJson
    {
        public ProjectDataJson()
        {
            Items = new List<CmdArgumentJson>();
        }
    }

    public class ProjectDataJsonVersioned : ProjectDataJson
    {
        public int FileVersion = 2;
    }

    public class CmdArgumentJson
    {
        public Guid Id = Guid.NewGuid();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Command = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProjectConfig = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProjectPlatform = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LaunchProfile = null;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ExclusiveMode = false;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate), DefaultValue(" ")]
        public string Delimiter = " ";
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<CmdArgumentJson> Items = null;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool DefaultChecked = false;

        [JsonIgnore]
        public bool Enabled = false;
        [JsonIgnore]
        public bool Expanded = false;
        [JsonIgnore]
        public bool Selected = false;

        [JsonIgnore]
        public IEnumerable<CmdArgumentJson> AllItems => Items
            .Concat(Items.Where(item => item.Items != null).SelectMany(container => container.AllItems));

        [JsonIgnore]
        public IEnumerable<CmdArgumentJson> AllArguments => AllItems.Where(item => item.Items == null);

        [JsonIgnore]
        public IEnumerable<CmdArgumentJson> AllContainer => AllItems.Where(item => item.Items != null);

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
