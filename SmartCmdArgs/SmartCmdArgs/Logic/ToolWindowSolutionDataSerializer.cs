using Newtonsoft.Json;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartCmdArgs.Logic
{
    public class ToolWindowSolutionDataSerializer : ToolWindowDataSerializer
    {
        public static ToolWindowStateSolutionData Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<ToolWindowStateSolutionData>(jsonStr);

            return entries;
        }

        public static ToolWindowStateSolutionData Serialize(ToolWindowViewModel vm, Stream stream)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = new ToolWindowStateSolutionData();

            data.CheckedArguments = new HashSet<Guid>(vm.TreeViewModel.Projects.Values.SelectMany(p => p.CheckedArguments).Select(arg => arg.Id));
            data.ExpandedContainer = new HashSet<Guid>(vm.TreeViewModel.Projects.Values.SelectMany(p  => p.ExpandedContainer).Select(con => con.Id)
                                               .Concat(vm.TreeViewModel.Projects.Values.Where(p => p.IsExpanded).Select(p => p.Id)));

            foreach (var kvPair in vm.TreeViewModel.Projects)
            {
                var list = new ToolWindowStateProjectData
                {
                    Id = kvPair.Value.Id,
                    Items = TransformCmdList(kvPair.Value.Items)
                };
                data.ProjectArguments.Add(kvPair.Key, list);
            }

            string jsonStr = JsonConvert.SerializeObject(data);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();

            return data;
        }
    }
}
