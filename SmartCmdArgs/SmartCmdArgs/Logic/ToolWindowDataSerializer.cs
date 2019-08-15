using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Logic
{
    class ToolWindowDataSerializer
    {
        protected static List<ListEntryData> TransformCmdList(ICollection<CmdBase> items)
        {
            var result = new List<ListEntryData>(items.Count);
            foreach (var item in items)
            {
                var newElement = new ListEntryData
                {
                    Id = item.Id,
                    Command = item.Value,
                    ProjectConfig = item.ProjectConfig,
                    LaunchProfile = item.LaunchProfile,

                    // not in JSON
                    Selected = item.IsSelected,
                };

                if (item is CmdArgument arg)
                {
                    // not in JSON
                    newElement.Enabled = item.IsChecked ?? false;
                }

                if (item is CmdContainer container)
                {
                    newElement.Items = TransformCmdList(container.Items);
                    newElement.ExclusiveMode = container.ExclusiveMode;

                    // not in JSON
                    newElement.Expanded = container.IsExpanded;
                }

                result.Add(newElement);
            }
            return result;
        }
    }
}
