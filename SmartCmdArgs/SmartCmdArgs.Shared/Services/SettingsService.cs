using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Services
{
    public interface ISettingsService
    {
        bool Loaded { get; }

        void Load();
        void Save();
        void Reset();
    }

    internal class SettingsService : ISettingsService
    {
        private readonly SettingsViewModel settingsViewModel;
        private readonly IFileStorageService fileStorage;
        private readonly ISuoDataService suoData;

        public bool Loaded { get; set; }

        public SettingsService(SettingsViewModel settingsViewModel, IFileStorageService fileStorage, ISuoDataService suoData)
        {
            this.settingsViewModel = settingsViewModel;
            this.fileStorage = fileStorage;
            this.suoData = suoData;
        }

        public void Save()
        {
            fileStorage.SaveSettings();
        }

        public void Load()
        {
            var settings = fileStorage.ReadSettings();

            var areSettingsFromFile = settings != null;

            if (settings == null)
                settings = suoData.SuoDataJson.Settings;

            if (settings == null)
                settings = new SettingsJson();

            settingsViewModel.Assign(settings);
            settingsViewModel.SaveSettingsToJson = areSettingsFromFile;

            Loaded = true;
        }

        public void Reset()
        {
            Loaded = false;
        }
    }
}
