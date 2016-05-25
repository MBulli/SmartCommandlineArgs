using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Logic
{
    class ToolWindowProjectDataSerializer
    {
        public static void Serialize(ListViewModel vm, Stream stream)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = new ToolWindowStateProjectData();

            foreach (var item in vm.DataCollection)
            {
                data.DataCollection.Add(new ToolWindowStateProjectData.ListEntryData()
                {
                    Id = item.Id,
                    Command = item.Command,
                    //Project = item.Project,   // deprecated
                    //Enabled = item.Enabled
                });
            }

            string jsonStr = JsonConvert.SerializeObject(data, Formatting.Indented);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();
        }

        public static ToolWindowStateProjectData Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<ToolWindowStateProjectData>(jsonStr);

            return entries;
        }
    }
}
