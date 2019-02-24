using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs
{
    public class CmdArgsOptionPage : DialogPage
    {
        private bool _vcsSupport = true;
        private bool _macroEvaluation = true;
        private bool _useMonospaceFont = false;

        [Category("General")]
        [DisplayName("Enable version control support")]
        [Description("If enabled the extension will store the command line arguments into an json file at the same loctation as the related project file. That way the command line arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions .suo-file which is usally ignored by version control. The default value for this setting is True.")]
        public bool VcsSupport
        {
            get => _vcsSupport;
            set
            {
                var oldValue = _vcsSupport;
                _vcsSupport = value;
                if (oldValue != value)
                    VcsSupportChanged?.Invoke(this, value);
            }
        }

        [Category("General")]
        [DisplayName("Enable Macro evaluation")]
        [Description("If enabled Macros like '$(ProjectDir)' will be evaluated and replaced by the corresponding string.")]
        public bool MacroEvaluation
        {
            get => _macroEvaluation;
            set
            {
                var oldValue = _macroEvaluation;
                _macroEvaluation = value;
                if (oldValue != value)
                    MacroEvaluationChanged?.Invoke(this, value);
            }
        }
        
        [Category("General")]
        [DisplayName("Use Monospace Font")]
        [Description("If enabled the fontfamily is changed to 'Consolas'.")]
        public bool UseMonospaceFont
        {
            get => _useMonospaceFont;
            set
            {
                var oldValue = _useMonospaceFont;
                _useMonospaceFont = value;
                if (oldValue != value)
                    UseMonospaceFontChanged?.Invoke(this, value);
            }
        }

        public override void ResetSettings()
        {
            base.ResetSettings();

            VcsSupport = true;
            MacroEvaluation = true;
        }

        public event EventHandler<bool> VcsSupportChanged;
        public event EventHandler<bool> MacroEvaluationChanged;
        public event EventHandler<bool> UseMonospaceFontChanged;
    }
}
