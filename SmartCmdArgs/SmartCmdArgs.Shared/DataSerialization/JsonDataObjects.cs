using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.DataSerialization
{
    public class SettingsJson
    {
        public bool? ManageCommandLineArgs { get; set; }
        public bool? ManageEnvironmentVars { get; set; }
        public bool? ManageWorkingDirectories { get; set; }
        public bool? ManageLaunchApplication { get; set; }
        public bool UseCustomJsonRoot { get; set; }
        public string JsonRootPath { get; set; }
        public bool? AutoProfileUpdates { get; set; }
        public bool? VcsSupportEnabled { get; set; }
        public bool? UseSolutionDir { get; set; }
        public bool? MacroEvaluationEnabled { get; set; }

        public SettingsJson() { }

        public SettingsJson(SettingsViewModel settingsViewModel)
        {
            ManageCommandLineArgs = settingsViewModel.ManageCommandLineArgs;
            ManageEnvironmentVars = settingsViewModel.ManageEnvironmentVars;
            ManageWorkingDirectories = settingsViewModel.ManageWorkingDirectories;
            ManageLaunchApplication = settingsViewModel.ManageLaunchApplication;
            AutoProfileUpdates = settingsViewModel.AutoProfileUpdates;
            VcsSupportEnabled = settingsViewModel.VcsSupportEnabled;
            UseSolutionDir = settingsViewModel.UseSolutionDir;
            MacroEvaluationEnabled = settingsViewModel.MacroEvaluationEnabled;
            UseCustomJsonRoot = settingsViewModel.UseCustomJsonRoot;

            if (UseCustomJsonRoot)
                JsonRootPath = settingsViewModel.JsonRootPath;
        }
    }

    public class SuoDataJson
    {
        public int FileVersion = 2;

        public bool? IsEnabled;
        public bool ShowAllProjects;
        public HashSet<Guid> SelectedItems = new HashSet<Guid>();
        public HashSet<Guid> CheckedArguments = new HashSet<Guid>();
        public HashSet<Guid> ExpandedContainer = new HashSet<Guid>();
        
        public Dictionary<Guid, ProjectDataJson> ProjectArguments = new Dictionary<Guid, ProjectDataJson>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SettingsJson Settings = new SettingsJson();
    }

    public class SolutionDataJson
    {
        public int FileVersion = 2;

        public List<ProjectDataJson> ProjectArguments = new List<ProjectDataJson>();
    }

    public class ProjectDataJson : CmdItemJson
    {
        public ProjectDataJson()
        {
            Items = new List<CmdItemJson>();
        }
    }

    public class ProjectDataJsonVersioned : ProjectDataJson
    {
        public int FileVersion = 2;
    }

    public class CmdItemJson
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
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore), DefaultValue("")]
        public string Postfix = "";
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore), DefaultValue("")]
        public string Prefix = "";
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<CmdItemJson> Items = null;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool DefaultChecked = false;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate), DefaultValue(CmdParamType.CmdArg)]
        public CmdParamType Type = CmdParamType.CmdArg;

        [JsonIgnore]
        public bool Enabled = false;
        [JsonIgnore]
        public bool Expanded = false;
        [JsonIgnore]
        public bool Selected = false;

        [JsonIgnore]
        public IEnumerable<CmdItemJson> AllItems => Items
            .Concat(Items.Where(item => item.Items != null).SelectMany(container => container.AllItems));

        [JsonIgnore]
        public IEnumerable<CmdItemJson> AllParameters => AllItems.Where(item => item.Items == null);

        [JsonIgnore]
        public IEnumerable<CmdItemJson> AllContainer => AllItems.Where(item => item.Items != null);

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
