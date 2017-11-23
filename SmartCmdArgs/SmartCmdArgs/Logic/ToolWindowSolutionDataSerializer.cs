using Newtonsoft.Json;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Logic
{
    public class ToolWindowSolutionDataSerializer
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

            foreach (var kvPair in vm.TreeViewModel.Projects)
            {
                var list = new ToolWindowStateProjectData();
                data.Add(kvPair.Key, list);

                foreach (var item in kvPair.Value.Items)
                {
                    list.DataCollection.Add(new ToolWindowStateProjectData.ListEntryData()
                    {
                        Id = item.Id,
                        Command = item.Value,
                        //Project = item.Project,   // deprecated
                        Enabled = item.IsChecked == true
                    });
                }
            }

            string jsonStr = JsonConvert.SerializeObject(data);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();

            return data;
        }
    }
}
