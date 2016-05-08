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
        [Category("General")]
        [DisplayName("Source version control support")]
        [Description("Lets you enable or disable svc support.")] // TODO wordly description
        public bool SvcSupport { get; set; }
    }
}
