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
        [DisplayName("Enable version control support")]
        [Description("If enabled the extension will store the commandline arguments into an json file at the same loctation as the related project file. That way the commandline arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions .suo-file which is usally ignored by version control. The default value for this setting is True.")]
        public bool SvcSupport { get; set; }
    }
}
