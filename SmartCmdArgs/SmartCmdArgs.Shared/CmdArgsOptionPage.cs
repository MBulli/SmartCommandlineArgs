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
        private bool _useMonospaceFont = false;
        
        [Category("General")]
        [DisplayName("Use Monospace Font")]
        [Description("If enabled the fontfamily is changed to 'Consolas'.")]
        [DefaultValue(false)]
        public bool UseMonospaceFont
        {
            get => _useMonospaceFont;
            set
            {
                if (_useMonospaceFont != value)
                {
                    _useMonospaceFont = value;
                    UseMonospaceFontChanged?.Invoke(this, value);
                }
            }
        }

        public override void ResetSettings()
        {
            base.ResetSettings();
            
            UseMonospaceFont = false;
        }
        
        public event EventHandler<bool> UseMonospaceFontChanged;
    }
}
