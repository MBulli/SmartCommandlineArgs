using SmartCmdArgs.Logic;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Services
{
    public interface ISettingsService
    {
        SettingsViewModel ViewModel { get; }

        void Load();
        void Save();
    }

    internal class SettingsService : ISettingsService
    {
        private readonly SettingsViewModel settingsViewModel;
        private readonly IFileStorageService fileStorage;
        private readonly ISuoDataService suoData;

        public SettingsViewModel ViewModel => settingsViewModel;

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
        }
    }
}
