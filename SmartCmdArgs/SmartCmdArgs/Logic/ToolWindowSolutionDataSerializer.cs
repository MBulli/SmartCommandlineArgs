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
            if (vsHelper == null)
                throw new ArgumentNullException(nameof(vsHelper));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            return Deserialize(jsonStr, vsHelper);
        }

        public static ToolWindowStateSolutionData Deserialize(string jsonStr, VisualStudioHelper vsHelper)
        {
            if (vsHelper == null)
                throw new ArgumentNullException(nameof(vsHelper));

            Logger.Info($"Try to parse solution json: '{jsonStr}'");

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty solution data
                Logger.Info("Got empty solution json string. Returning empty ToolWindowStateSolutionData");
                return new ToolWindowStateSolutionData();
            }

            // At the moment there are two json formats.
            // The 'old' format and the new one.
            // The FileVersion property was introduced with the new format
            // Hence, a missing FileVersion indicates the old format.
            var obj = JObject.Parse(jsonStr);
            int fileVersion = ((int?)obj["FileVersion"]).GetValueOrDefault();
            Logger.Info($"Solution json file version is '{fileVersion}'");

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
                Logger.Warn($"Failed to parse solution json with exception: '{e}'");
                return new ToolWindowStateSolutionData();
            }
        }

        private static ToolWindowStateSolutionData ParseOldJsonFormat(JObject obj, VisualStudioHelper vsHelper)
        {
            var result = new ToolWindowStateSolutionData();

            foreach (var prop in obj.Properties())
            {
                var projectState = ToolWindowProjectDataSerializer.ParseOldJsonFormat(prop.Value);

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

        public static ToolWindowStateSolutionData Serialize(ToolWindowViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            var data = new ToolWindowStateSolutionData();

            data.ShowAllProjects = vm.TreeViewModel.ShowAllProjects;
            data.CheckedArguments = new HashSet<Guid>(vm.TreeViewModel.AllProjects.SelectMany(p => p.CheckedArguments).Select(arg => arg.Id));
            data.ExpandedContainer = new HashSet<Guid>(vm.TreeViewModel.AllItems.OfType<CmdContainer>().Where(con => con.IsExpanded).Select(p => p.Id));

            data.SelectedItems = new HashSet<Guid>(vm.TreeViewModel.Projects.Values.SelectMany(p => p.SelectedItems).Select(item => item.Id)
                                                   .Concat(vm.TreeViewModel.Projects.Values.Where(p => p.IsSelected).Select(p => p.Id)));

            foreach (var kvPair in vm.TreeViewModel.Projects)
            {
                var list = new ToolWindowStateProjectData
                {
                    Id = kvPair.Value.Id,
                    ExclusiveMode = kvPair.Value.ExclusiveMode,
                    Items = TransformCmdList(kvPair.Value.Items),

                    // not in JSON
                    Expanded = kvPair.Value.IsExpanded,
                    Selected = kvPair.Value.IsSelected,
                };
                data.ProjectArguments.Add(kvPair.Key, list);
            }

            return data;
        }

        public static ToolWindowStateSolutionData Serialize(ToolWindowViewModel vm, Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var data = Serialize(vm);

            string jsonStr = JsonConvert.SerializeObject(data);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();

            return data;
        }
    }
}
