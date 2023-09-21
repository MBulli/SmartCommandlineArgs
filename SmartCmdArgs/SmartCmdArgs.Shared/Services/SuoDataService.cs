﻿using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;
using System;
using System.IO;

namespace SmartCmdArgs.Services
{
    internal interface ISuoDataService
    {
        SuoDataJson SuoDataJson { get; }

        void LoadFromStream(Stream stream);
        void Deserialize();
        void SaveToStream(Stream stream);
        void Update();
        void Reset();

    }

    internal class SuoDataService : ISuoDataService
    {
        private readonly IVisualStudioHelperService visualStudioHelper;
        private readonly Lazy<ToolWindowViewModel> toolWindowViewModel;
        private readonly Lazy<SettingsViewModel> settingsViewModel;
        private readonly ILifeCycleService lifeCycleService;

        // We store the commandline arguments also in the suo file.
        // This is handled in the OnLoad/SaveOptions methods.
        // As the parser needs a initialized instance of vsHelper,
        // the json string from the suo is saved in this variable and
        // processed later.
        private string suoDataStr;
        private SuoDataJson suoDataJson;

        public SuoDataJson SuoDataJson => suoDataJson;

        public SuoDataService(
            IVisualStudioHelperService visualStudioHelper,
            Lazy<ToolWindowViewModel> toolWindowViewModel,
            Lazy<SettingsViewModel> settingsViewModel,
            ILifeCycleService lifeCycleService)
        {
            this.visualStudioHelper = visualStudioHelper;
            this.toolWindowViewModel = toolWindowViewModel;
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
            suoDataJson = SuoDataSerializer.Deserialize(suoDataStr, visualStudioHelper);
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
            suoDataJson = SuoDataSerializer.Serialize(toolWindowViewModel.Value, settingsViewModel.Value);
            suoDataJson.IsEnabled = lifeCycleService.IsEnabledSaved;

            suoDataStr = JsonConvert.SerializeObject(suoDataJson);
        }

        public void Reset()
        {
            suoDataStr = "";
            suoDataJson = null;
        }
    }
}
