using Microsoft.VisualStudio.ProjectSystem;
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
        private bool _deleteEmptyFilesAutomatically = true;
        private bool _deleteUnnecessaryFilesAutomatically = true;

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

        [Category("Cleanup")]
        [DisplayName("Delete empty files automatically")]
        [Description("If enabled, '*.args.json' files which would contain no arguments will be delete automatically.")]
        [DefaultValue(true)]
        public bool DeleteEmptyFilesAutomatically
        {
            get => _deleteEmptyFilesAutomatically;
            set
            {
                if (_deleteEmptyFilesAutomatically != value)
                {
                    _deleteEmptyFilesAutomatically = value;
                    DeleteEmptyFilesAutomaticallyChanged?.Invoke(this, value);
                }
            }
        }

        [Category("Cleanup")]
        [DisplayName("Delete unnecessary files automatically")]
        [Description("If enabled, '*.args.json' whcih are unnecessary will be deleted automatically. Such a file is unnecessary if it belongs to a project and 'Use Solution Directory' is enbaled or belongs to a solution and 'Use Solution Directory' is disabled.")]
        [DefaultValue(true)]
        public bool DeleteUnnecessaryFilesAutomatically
        {
            get => _deleteUnnecessaryFilesAutomatically;
            set
            {
                if (_deleteUnnecessaryFilesAutomatically != value)
                {
                    _deleteUnnecessaryFilesAutomatically = value;
                    DeleteUnnecessaryFilesAutomaticallyChanged?.Invoke(this, value);
                }
            }
        }

        public override void ResetSettings()
        {
            base.ResetSettings();
            
            UseMonospaceFont = false;
            DeleteEmptyFilesAutomatically = true;
            DeleteUnnecessaryFilesAutomatically = true;
        }
        
        public event EventHandler<bool> UseMonospaceFontChanged;
        public event EventHandler<bool> DeleteEmptyFilesAutomaticallyChanged;
        public event EventHandler<bool> DeleteUnnecessaryFilesAutomaticallyChanged;
    }
}
