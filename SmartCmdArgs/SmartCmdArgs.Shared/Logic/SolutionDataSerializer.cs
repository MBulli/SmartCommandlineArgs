using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Logic
{
    class SolutionDataSerializer : DataSerializer
    {
        public static SolutionDataJson Serialize(TreeViewModel vm, Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = Serialize(vm);

            string jsonStr = JsonConvert.SerializeObject(data, Formatting.Indented);

            StreamWriter sw = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            sw.Write(jsonStr);
            sw.Flush();

            return data;
        }

        public static SolutionDataJson Serialize(TreeViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            var data = new SolutionDataJson();

            foreach (var kvPair in vm.Projects)
            {
                var list = new ProjectDataJson
                {
                    Id = kvPair.Value.Id,
                    ExclusiveMode = kvPair.Value.ExclusiveMode,
                    Delimiter = kvPair.Value.Delimiter,
                    Items = TransformCmdList(kvPair.Value.Items),

                    // not in JSON
                    Expanded = kvPair.Value.IsExpanded,
                    Selected = kvPair.Value.IsSelected,
                };
                data.ProjectArguments.Add(list);
            }

            return data;
        }

        public static SolutionDataJson Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            return Deserialize(jsonStr);
        }

        public static SolutionDataJson Deserialize(string jsonStr)
        {
            Logger.Info($"Try to parse solution json: '{jsonStr}'");

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty solution data
                Logger.Info("Got empty solution json string. Returning empty SolutionDataJson");
                return new SolutionDataJson();
            }

            // This class came later. Thus theres only version 2 yet.
            var obj = JObject.Parse(jsonStr);
            int fileVersion = ((int?)obj["FileVersion"]).GetValueOrDefault();
            Logger.Info($"Solution json file version is '{fileVersion}'");

            try
            {
                var slnData = JsonConvert.DeserializeObject<SolutionDataJson>(jsonStr);
                return slnData;
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to parse solution json with exception: '{e}'");
                return new SolutionDataJson();
            }
        }
    }
}
