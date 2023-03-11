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
    public class CmdArgsOptionPage : DialogPage, INotifyPropertyChanged
    {
        public CmdArgsOptionPage() : base()
        {
            ResetSettings();
        }

        private bool _useMonospaceFont;
        private bool _deleteEmptyFilesAutomatically;
        private bool _deleteUnnecessaryFilesAutomatically;

        private bool _saveSettingsToJson;
        private bool _useCustomJsonRoot;
        private string _jsonRootPath;
        private bool _vcsSupportEnabled;
        private bool _useSolutionDir;
        private bool _macroEvaluationEnabled;

        [Category("Appearance")]
        [DisplayName("Use Monospace Font")]
        [Description("If enabled the fontfamily is changed to 'Consolas'.")]
        [DefaultValue(false)]
        public bool UseMonospaceFont
        {
            get => _useMonospaceFont;
            set => SetAndNotify(value, ref _useMonospaceFont);
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
        [DisplayName("Save Settings to JSON")]
        [Description("If enabled then the settings configured here are saved to a JSON file.")]
        [DefaultValue(false)]
        public bool SaveSettingsToJson
        {
            get => _saveSettingsToJson;
            set => SetAndNotify(value, ref _saveSettingsToJson);
        }

        [Category("Settings Defaults")]
        [DisplayName("Save Settings to custom Root path")]
        [Description("If enabled then the settings configured here are saved to a JSON file within this root path.")]
        [DefaultValue(false)]
        public bool UseCustomJsonRoot
        {
            get => _useCustomJsonRoot;
            set => SetAndNotify(value, ref _useCustomJsonRoot);
        }

        [Category("Settings Defaults")]
        [DisplayName("JSON Root Path")]
        [Description("The Root Path to store JSON Settings files. If empty, files will be stored at the same location as the related project file.")]
        [DefaultValue("")]
        public string JsonRootPath
        {
            get => _jsonRootPath;
            set => SetAndNotify(value, ref _jsonRootPath);
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
