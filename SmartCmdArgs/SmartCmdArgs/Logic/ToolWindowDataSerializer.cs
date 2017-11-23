using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Logic
{
    public class ToolWindowDataSerializer
    {
        protected static List<ToolWindowStateProjectData.ListEntryData> TransformCmdList(ICollection<CmdBase> items)
        {
            var result = new List<ToolWindowStateProjectData.ListEntryData>(items.Count);
            foreach (var item in items)
            {
                var newElement = new ToolWindowStateProjectData.ListEntryData{ Command = item.Value };

                if (item is CmdArgument argument)
                    newElement.Id = argument.Id;
                else if (item is CmdContainer container)
                    newElement.Items = TransformCmdList(container.Items);

                result.Add(newElement);
            }
            return result;
        }
    }
}
