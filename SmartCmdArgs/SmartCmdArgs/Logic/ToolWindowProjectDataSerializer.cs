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
        public static void Serialize(CmdProject prj, Stream stream)
        {
            if (prj == null)
                throw new ArgumentNullException(nameof(prj));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = new ToolWindowStateProjectData();

            foreach (var item in prj.Items)
            {
                data.DataCollection.Add(new ToolWindowStateProjectData.ListEntryData()
                {
                    Id = item.Id,
                    Command = item.Value,
                    //Project = item.Project,   // deprecated
                    //Enabled = item.Enabled
                });
            }

            string jsonStr = JsonConvert.SerializeObject(data, Formatting.Indented);

            StreamWriter sw = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            sw.Write(jsonStr);
            sw.Flush();
        }

        public static ToolWindowStateProjectData Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            string jsonStr = sr.ReadToEnd();

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty project data
                return new ToolWindowStateProjectData();
            }
            else
            {
                var entries = JsonConvert.DeserializeObject<ToolWindowStateProjectData>(jsonStr);
                return entries;
            }
        }
    }
}
