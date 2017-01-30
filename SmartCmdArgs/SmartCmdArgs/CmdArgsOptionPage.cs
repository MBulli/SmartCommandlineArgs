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

        [Category("General")]
        [DisplayName("Enable version control support")]
        [Description("If enabled the extension will store the commandline arguments into an json file at the same loctation as the related project file. That way the commandline arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions .suo-file which is usally ignored by version control. The default value for this setting is True.")]
        public bool VcsSupport
        {
            get { return _vcsSupport; }
            set
            {
                if (_vcsSupport != value)
                    VcsSupportChanged?.Invoke(this, value);
                _vcsSupport = value;
            }
        }

        [Category("General")]
        [DisplayName("Enable Macro evaluation")]
        [Description("If enabled Macros like '$(ProjectDir)' will be evaluated and replaced by the corresponding string.")]
        public bool MacroEvaluation
        {
            get { return _macroEvaluation; }
            set
            {
                if (_macroEvaluation != value)
                    MacroEvaluationChanged?.Invoke(this, value);
                _macroEvaluation = value;
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
    }
}
