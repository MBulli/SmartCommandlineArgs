using Newtonsoft.Json;
using SmartCmdArgs.Helper;
using SmartCmdArgs.DataSerialization;
using SmartCmdArgs.ViewModel;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SmartCmdArgs.Services
{
    internal interface ISuoDataService
    {
        SuoDataJson SuoDataJson { get; }
        HashSet<Guid> ParametersOfSuoData { get; }

        void LoadFromStream(Stream stream);
        void Deserialize();
        void SaveToStream(Stream stream);
        void Update();
        void Reset();

    }

    internal class SuoDataService : ISuoDataService
    {
        private readonly IVisualStudioHelperService visualStudioHelper;
        private readonly TreeViewModel treeViewModel;
        private readonly Lazy<SettingsViewModel> settingsViewModel;
        private readonly Lazy<ILifeCycleService> lifeCycleService;

        // We store the commandline arguments also in the suo file.
        // This is handled in the OnLoad/SaveOptions methods.
        // As the parser needs a initialized instance of vsHelper,
        // the json string from the suo is saved in this variable and
        // processed later.
        private string suoDataStr;

        public HashSet<Guid> ParametersOfSuoData { get; } = new HashSet<Guid>();

        private SuoDataJson suoDataJson;
        public SuoDataJson SuoDataJson
        {
            get => suoDataJson;
            private set
            {
                ParametersOfSuoData.Clear();
                suoDataJson = value;

                if (suoDataJson != null)
                {
                    ParametersOfSuoData.AddRange(suoDataJson.ProjectArguments.Values.SelectMany(x => x.AllParameters).Select(x => x.Id));
                }
            }
        }

        public SuoDataService(
            IVisualStudioHelperService visualStudioHelper,
            TreeViewModel treeViewModel,
            Lazy<SettingsViewModel> settingsViewModel,
            Lazy<ILifeCycleService> lifeCycleService)
        {
            this.visualStudioHelper = visualStudioHelper;
            this.treeViewModel = treeViewModel;
            this.settingsViewModel = settingsViewModel;
            this.lifeCycleService = lifeCycleService;
        }

        public void LoadFromStream(Stream stream)
        {
            StreamReader sr = new StreamReader(stream); // don't free
            suoDataStr = sr.ReadToEnd();
        }

        public void Deserialize()
        {
            SuoDataJson = SuoDataSerializer.Deserialize(suoDataStr, visualStudioHelper);
        }

        public void SaveToStream(Stream stream)
        {
            Logger.Info("Saving commands to suo file.");
            StreamWriter sw = new StreamWriter(stream);
            sw.Write(suoDataStr);
            sw.Flush();
            Logger.Info("All Commands saved to suo file.");
        }

        public void Update()
        {
            SuoDataJson = SuoDataSerializer.Serialize(treeViewModel, settingsViewModel.Value);
            SuoDataJson.IsEnabled = lifeCycleService.Value.IsEnabledSaved;

            suoDataStr = JsonConvert.SerializeObject(SuoDataJson);
        }

        public void Reset()
        {
            suoDataStr = "";
            SuoDataJson = null;
        }
    }
}
