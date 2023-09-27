using SmartCmdArgs.ViewModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SmartCmdArgs.Services
{
    public interface ILifeCycleService : INotifyPropertyChanged
    {
        bool IsEnabled { get; }
        bool? IsEnabledSaved { get; set; }

        void FinalizeConfigForSolution();
        void InitializeConfigForSolution();
        void UpdateDisabledScreen();
    }

    internal class LifeCycleService : ILifeCycleService
    {
        private readonly ISuoDataService suoDataService;
        private readonly ISettingsService settingsService;
        private readonly ToolWindowViewModel toolWindowViewModel;
        private readonly TreeViewModel treeViewModel;
        private readonly IVisualStudioHelperService vsHelper;
        private readonly IViewModelUpdateService viewModelUpdateService;
        private readonly IFileStorageService fileStorage;
        private readonly IOptionsSettingsService optionsSettings;
        private readonly IVsEventHandlingService vsEventHandling;
        private readonly IOptionsSettingsEventHandlingService optionsSettingsEventHandling;
        private readonly ITreeViewEventHandlingService treeViewEventHandling;


        // this is needed to keep the saved value in the suo file at null
        // if the user does not explicitly enable or disable the extension
        private bool? _isEnabledSaved;
        public bool? IsEnabledSaved
        {
            get => _isEnabledSaved;
            set
            {
                _isEnabledSaved = value;
                IsEnabled = value ?? GetDefaultEnabled();
            }
        }

        /// <summary>
        /// While the extension is disabled we do nothing.
        /// The user is asked to enable the extension.
        /// This solves the issue that the extension accidentilly overrides user changes.
        /// 
        /// If this changes the updated value is not written to the suo file.
        /// For that `IsEnabledSaved` has to be updated.
        /// </summary>
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            private set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    IsEnabledChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LifeCycleService(
            ISuoDataService suoDataService,
            ISettingsService settingsService,
            ToolWindowViewModel toolWindowViewModel,
            TreeViewModel treeViewModel,
            IVisualStudioHelperService vsHelper,
            IViewModelUpdateService viewModelUpdateService,
            IFileStorageService fileStorage,
            IOptionsSettingsService optionsSettings,
            IVsEventHandlingService vsEventHandling,
            IOptionsSettingsEventHandlingService optionsSettingsEventHandling,
            ITreeViewEventHandlingService treeViewEventHandling)
        {
            this.suoDataService = suoDataService;
            this.settingsService = settingsService;
            this.toolWindowViewModel = toolWindowViewModel;
            this.treeViewModel = treeViewModel;
            this.vsHelper = vsHelper;
            this.viewModelUpdateService = viewModelUpdateService;
            this.fileStorage = fileStorage;
            this.optionsSettings = optionsSettings;
            this.vsEventHandling = vsEventHandling;
            this.optionsSettingsEventHandling = optionsSettingsEventHandling;
            this.treeViewEventHandling = treeViewEventHandling;
        }

        private bool GetDefaultEnabled()
        {
            switch(optionsSettings.EnableBehaviour)
            {
                case EnableBehaviour.EnableByDefault: 
                    return true;

                case EnableBehaviour.AlwaysAsk: 
                    return false;

                case EnableBehaviour.EnableIfArgJsonIsFound: 
                    return optionsSettings.VcsSupportEnabled 
                        && fileStorage.GetAllArgJsonFileNames().Any(File.Exists);

                default: return false;
            }
        }

        public void UpdateDisabledScreen()
        {
            toolWindowViewModel.ShowDisabledScreen = !IsEnabled && vsHelper.IsSolutionOpen;
        }

        internal void AttachToEvents()
        {
            // events registered here are only called while the extension is enabled

            vsEventHandling.AttachToProjectEvents();
            optionsSettingsEventHandling.AttachToEvents();
            treeViewEventHandling.AttachToEvents();
        }

        internal void DetachFromEvents()
        {
            // all events regitered in AttachToEvents should be unregisterd here

            vsEventHandling.DetachFromProjectEvents();
            optionsSettingsEventHandling.DetachFromEvents();
            treeViewEventHandling.DetachFromEvents();
        }

        private void IsEnabledChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));

            UpdateDisabledScreen();

            if (IsEnabled)
            {
                AttachToEvents();
                InitializeDataForSolution();
            }
            else
            {
                // captures the state right after disableing the extension
                // changes after this point are ignored
                suoDataService.Update();
                DetachFromEvents();
                FinalizeDataForSolution();
            }
        }

        public void InitializeConfigForSolution()
        {
            suoDataService.Deserialize();

            settingsService.Load();

            IsEnabledSaved = suoDataService.SuoDataJson.IsEnabled;
        }

        private void InitializeDataForSolution()
        {
            Debug.Assert(IsEnabled);

            treeViewModel.ShowAllProjects = suoDataService.SuoDataJson.ShowAllProjects;

            foreach (var project in vsHelper.GetSupportedProjects())
            {
                viewModelUpdateService.UpdateCommandsForProject(project);
                fileStorage.AddProject(project);
            }
            viewModelUpdateService.UpdateCurrentStartupProject();
            viewModelUpdateService.UpdateIsActiveForParamsDebounced();
        }

        private void FinalizeDataForSolution()
        {
            Debug.Assert(!IsEnabled);

            fileStorage.RemoveAllProjects();
            toolWindowViewModel.Reset();
        }

        public void FinalizeConfigForSolution()
        {
            IsEnabled = false;
            UpdateDisabledScreen();
            suoDataService.Reset();
            settingsService.Reset();
        }
    }
}
