using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Logic
{
    public class ToolWindowStateSolutionData : Dictionary<string, ToolWindowStateProjectData>
    {}

    public class ToolWindowStateProjectData
    {
        public List<ListEntryData> DataCollection = new List<ListEntryData>();

        public class ListEntryData
        {
            public Guid Id;
            public string Command;
            public string Project; // this one is useles
            public bool Enabled;
        }
    }

    
}
