using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs
{
    public enum RelativePathRootOption
    {
        BuildTargetDirectory,
        ProjectDirectory
    }

    public enum InactiveDisableMode
    {
        Disabled,
        InAllProjects,
        OnlyInStartupProjects,
    }

    public class CmdArgsOptionPage : DialogPage, INotifyPropertyChanged
    {
        private bool _dontSave = false;

        public CmdArgsOptionPage() : base()
        {
            _dontSave = true;
            try
            {
                ResetSettings();
            }
            finally
            {
                _dontSave = false;
            }
        }

        private RelativePathRootOption _relativePathRoot;

        private bool _useMonospaceFont;
        private bool _displayTagForCla;
        private InactiveDisableMode _disableInactiveItems;

        private bool _deleteEmptyFilesAutomatically;
        private bool _deleteUnnecessaryFilesAutomatically;

        private bool _manageCommandLineArgs;
        private bool _manageEnvironmentVars;
        private bool _manageLaunchApplication;
        private bool _vcsSupportEnabled;
        private bool _useSolutionDir;
        private bool _macroEvaluationEnabled;

        [Category("General")]
        [DisplayName("Relative path root")]
        [Description("Sets the base path that is used to resolve relative paths for the open/reveal file/folder context menu option.")]
        [DefaultValue(RelativePathRootOption.BuildTargetDirectory)]
        public RelativePathRootOption RelativePathRoot
        {
            get => _relativePathRoot;
            set => SetAndNotify(value, ref _relativePathRoot);
        }

        [Category("Appearance")]
        [DisplayName("Use Monospace Font")]
        [Description("If enabled the fontfamily is changed to 'Consolas'.")]
        [DefaultValue(false)]
        public bool UseMonospaceFont
        {
            get => _useMonospaceFont;
            set => SetAndNotify(value, ref _useMonospaceFont);
        }

        [Category("Appearance")]
        [DisplayName("Display Tags for CLAs")]
        [Description("If enabled the item tag 'CLA' is displayed for Command Line Arguments. Normally the tag 'ENV' is only displayed for environment varibales.")]
        [DefaultValue(false)]
        public bool DisplayTagForCla
        {
            get => _displayTagForCla;
            set => SetAndNotify(value, ref _displayTagForCla);
        }

        [Category("Appearance")]
        [DisplayName("Grey out inactive items")]
        [Description("If set to 'Disabled' nothing happens. If set to 'InAllProjects' then CLAs and EnvVars that are not applied in the current scenario are greyed out. E.g. arguments that are in a group for a project configuration that is currently not active or environment variables that are overridden somewhere later in the list. The 'OnlyInStartupProjects' option limits this behaviour only to startup projects.")]
        [DefaultValue(InactiveDisableMode.Disabled)]
        public InactiveDisableMode DisableInactiveItems
        {
            get => _disableInactiveItems;
            set => SetAndNotify(value, ref _disableInactiveItems);
        }

        [Category("Cleanup")]
        [DisplayName("Delete empty files automatically")]
        [Description("If enabled, '*.args.json' files which would contain no arguments will be delete automatically.")]
        [DefaultValue(true)]
        public bool DeleteEmptyFilesAutomatically
        {
            get => _deleteEmptyFilesAutomatically;
            set => SetAndNotify(value, ref _deleteEmptyFilesAutomatically);
        }

        [Category("Cleanup")]
        [DisplayName("Delete unnecessary files automatically")]
        [Description("If enabled, '*.args.json' whcih are unnecessary will be deleted automatically. Such a file is unnecessary if it belongs to a project and 'Use Solution Directory' is enbaled or belongs to a solution and 'Use Solution Directory' is disabled.")]
        [DefaultValue(true)]
        public bool DeleteUnnecessaryFilesAutomatically
        {
            get => _deleteUnnecessaryFilesAutomatically;
            set => SetAndNotify(value, ref _deleteUnnecessaryFilesAutomatically);
        }

        [Category("Settings Defaults")]
        [DisplayName("Manage Command Line Arguments")]
        [Description("If enabled the arguments are set automatically when a project is started/debugged.")]
        [DefaultValue(true)]
        public bool ManageCommandLineArgs
        {
            get => _manageCommandLineArgs;
            set => SetAndNotify(value, ref _manageCommandLineArgs);
        }

        [Category("Settings Defaults")]
        [DisplayName("Manage Environment Variables")]
        [Description("If enabled the environment variables are set automatically when a project is started/debugged.")]
        [DefaultValue(false)]
        public bool ManageEnvironmentVars
        {
            get => _manageEnvironmentVars;
            set => SetAndNotify(value, ref _manageEnvironmentVars);
        }

        [Category("Settings Defaults")]
        [DisplayName("Manage Working Directories")]
        [Description("If enabled the working directories are set automatically when a project is started/debugged.")]
        [DefaultValue(false)]
        public bool ManageWorkingDirectories
        {
            get => _manageEnvironmentVars;
            set => SetAndNotify(value, ref _manageEnvironmentVars);
        }

        [Category("Settings Defaults")]
        [DisplayName("Manage Launch Application")]
        [Description("If enabled the launch application is set automatically when a project is started/debugged.")]
        [DefaultValue(false)]
        public bool ManageLaunchApplication
        {
            get => _manageLaunchApplication;
            set => SetAndNotify(value, ref _manageLaunchApplication);
        }

        [Category("Settings Defaults")]
        [DisplayName("Enable version control support")]
        [Description("If enabled the extension will store the command line arguments into an json file at the same loctation as the related project file. That way the command line arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions .suo-file which is usally ignored by version control. The default value for this setting is True.")]
        [DefaultValue(true)]
        public bool VcsSupportEnabled
        {
            get => _vcsSupportEnabled;
            set => SetAndNotify(value, ref _vcsSupportEnabled);
        }

        [Category("Settings Defaults")]
        [DisplayName("Use Solution Directory")]
        [Description("If enabled all arguments of every project will be stored in a single file next to the *.sln file. (Only if version control support is enabled)")]
        [DefaultValue(false)]
        public bool UseSolutionDir
        {
            get => _useSolutionDir;
            set => SetAndNotify(value, ref _useSolutionDir);
        }

        [Category("Settings Defaults")]
        [DisplayName("Enable Macro evaluation")]
        [Description("If enabled Macros like '$(ProjectDir)' will be evaluated and replaced by the corresponding string.")]
        [DefaultValue(true)]
        public bool MacroEvaluationEnabled
        {
            get => _macroEvaluationEnabled;
            set => SetAndNotify(value, ref _macroEvaluationEnabled);
        }

        public override void SaveSettingsToStorage()
        {
            if (_dontSave)
                return;

            base.SaveSettingsToStorage();
        }

        public override void ResetSettings()
        {
            base.ResetSettings();

            foreach (var prop in GetType().GetProperties())
            {
                var attribute = prop.GetCustomAttributes<DefaultValueAttribute>().FirstOrDefault();
                if (attribute != null)
                {
                    prop.SetValue(this, attribute.Value);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetAndNotify<T>(T newValue, ref T field, [CallerMemberName] string propertyName = null)
        {
            if (Equals(newValue, field)) return;
            field = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
