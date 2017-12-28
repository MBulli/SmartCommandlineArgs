using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;

namespace SmartCmdArgs.Logic
{
    class ToolWindowProjectDataSerializer : ToolWindowDataSerializer
    {
        public static void Serialize(CmdProject prj, Stream stream)
        {
            if (prj == null)
                throw new ArgumentNullException(nameof(prj));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = new ToolWindowStateProjectData
            {
                Id = prj.Id,
                Items = TransformCmdList(prj.Items)
            };

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

            Logger.Info($"Try to parse project json: '{jsonStr}'");

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty project data
                Logger.Info("Got empty project json string. Returning empty ToolWindowStateProjectData");
                return new ToolWindowStateProjectData();
            }
            else
            {
                var obj = JObject.Parse(jsonStr);
                int fileVersion = ((int?)obj["FileVersion"]).GetValueOrDefault();
                Logger.Info($"Project json file version is '{fileVersion}'");

                try
                {
                    if (fileVersion < 2)
                    {
                        return ParseOldJsonFormat(obj);
                    }
                    else
                    {
                        var entries = JsonConvert.DeserializeObject<ToolWindowStateProjectData>(jsonStr);
                        return entries;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn($"Failed to parse project json with exception: '{e}'");
                    return new ToolWindowStateProjectData();
                }
            }
        }

        public static ToolWindowStateProjectData ParseOldJsonFormat(JToken root)
        {
            var result = new ToolWindowStateProjectData();

            if (root is JObject)
            {
                foreach (var item in root["DataCollection"])
                {
                    var listItem = new ListEntryData();
                    result.Items.Add(listItem);

                    listItem.Command = (string)item["Command"];
                    listItem.Enabled = ((bool?)item["Enabled"]).GetValueOrDefault();

                    if (Guid.TryParse((string)item["Id"], out Guid parsedID))
                    {
                        listItem.Id = parsedID;
                    }
                }
            }

            return result;
        }
    }
}
