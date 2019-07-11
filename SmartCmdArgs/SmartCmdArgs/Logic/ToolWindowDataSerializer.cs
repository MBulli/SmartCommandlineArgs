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
                    LaunchProfile = item.LaunchProfile
                };
                
                if (item is CmdContainer container)
                {
                    newElement.Items = TransformCmdList(container.Items);
                    newElement.ExclusiveMode = container.ExclusiveMode;
                }

                result.Add(newElement);
            }
            return result;
        }
    }
}
