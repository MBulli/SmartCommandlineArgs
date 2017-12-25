using Newtonsoft.Json;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartCmdArgs.Logic
{
    class ToolWindowSolutionDataSerializer : ToolWindowDataSerializer
    {
        public static ToolWindowStateSolutionData Deserialize(Stream stream, VisualStudioHelper vsHelper)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            // At the moment there are two json formats.
            // The 'old' format and the new one.
            // The FileVersion property was introduced with the new format
            // Hence, a missing FileVersion indicates the old format.
            var obj = JObject.Parse(jsonStr);
            int fileVersion = ((int?)obj["FileVersion"]).GetValueOrDefault();

            try
            {
                if (fileVersion < 2)
                {
                    return ParseOldJsonFormat(obj, vsHelper);
                }
                else
                {
                    var entries = JsonConvert.DeserializeObject<ToolWindowStateSolutionData>(jsonStr);
                    return entries;
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Could not parse solution json. ({e.Message})");
                return null;
            }
        }

        private static ToolWindowStateSolutionData ParseOldJsonFormat(JObject obj, VisualStudioHelper vsHelper)
        {
            var result = new ToolWindowStateSolutionData();

            foreach (var prop in obj.Properties())
            {
                var projectState = ToolWindowProjectDataSerializer.ParseOldJosnFormat(prop.Value);

                var projectName = prop.Name;
                var projectGuid = vsHelper.ProjectGuidForProjetName(projectName);
                var enabledItems = from item in projectState.Items
                                   where item.Enabled
                                   select item.Id;

                result.ProjectArguments.Add(projectGuid, projectState);
                result.CheckedArguments.AddRange(enabledItems);               
            }

            return result;
        }

        public static ToolWindowStateSolutionData Serialize(ToolWindowViewModel vm, Stream stream)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = new ToolWindowStateSolutionData();

            data.ShowAllProjects = vm.TreeViewModel.ShowAllProjects;
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
