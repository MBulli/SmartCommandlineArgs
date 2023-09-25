using Newtonsoft.Json;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartCmdArgs.Services;

namespace SmartCmdArgs.DataSerialization
{
    class SuoDataSerializer : DataSerializer
    {
        public static SuoDataJson Deserialize(Stream stream, IVisualStudioHelperService vsHelper)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (vsHelper == null)
                throw new ArgumentNullException(nameof(vsHelper));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            return Deserialize(jsonStr, vsHelper);
        }

        public static SuoDataJson Deserialize(string jsonStr, IVisualStudioHelperService vsHelper)
        {
            if (vsHelper == null)
                throw new ArgumentNullException(nameof(vsHelper));

            Logger.Info($"Try to parse suo json: '{jsonStr}'");

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty solution data
                Logger.Info("Got empty suo json string. Returning empty SuoDataJson");
                return new SuoDataJson();
            }

            try
            {
                // At the moment there are two json formats.
                // The 'old' format and the new one.
                // The FileVersion property was introduced with the new format
                // Hence, a missing FileVersion indicates the old format.
                var obj = JObject.Parse(jsonStr);
                int fileVersion = ((int?)obj["FileVersion"]).GetValueOrDefault();
                Logger.Info($"Suo json file version is '{fileVersion}'");

                if (fileVersion < 2)
                {
                    return ParseOldJsonFormat(obj, vsHelper);
                }
                else
                {
                    var entries = JsonConvert.DeserializeObject<SuoDataJson>(jsonStr);
                    return entries;
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"Failed to parse suo json with exception: '{e}'");
                return new SuoDataJson();
            }
        }

        private static SuoDataJson ParseOldJsonFormat(JObject obj, IVisualStudioHelperService vsHelper)
        {
            var result = new SuoDataJson();

            foreach (var prop in obj.Properties())
            {
                var projectState = ProjectDataSerializer.ParseOldJsonFormat(prop.Value);

                var projectName = prop.Name;
                var projectGuid = vsHelper.HierarchyForProjectName(projectName).GetGuid();
                var enabledItems = from item in projectState.Items
                                   where item.Enabled
                                   select item.Id;

                result.ProjectArguments.Add(projectGuid, projectState);
                result.CheckedArguments.AddRange(enabledItems);               
            }

            return result;
        }

        public static SuoDataJson Serialize(TreeViewModel treeViewModel, SettingsViewModel settingsViewModel)
        {
            if (treeViewModel == null)
                throw new ArgumentNullException(nameof(treeViewModel));

            var data = new SuoDataJson();

            data.Settings = new SettingsJson(settingsViewModel);

            data.ShowAllProjects = treeViewModel.ShowAllProjects;
            data.CheckedArguments = new HashSet<Guid>(treeViewModel.AllProjects.SelectMany(p => p.CheckedArguments).Select(arg => arg.Id));
            data.ExpandedContainer = new HashSet<Guid>(treeViewModel.AllItems.OfType<CmdContainer>().Where(con => con.IsExpanded).Select(p => p.Id));

            data.SelectedItems = new HashSet<Guid>(treeViewModel.Projects.Values.SelectMany(p => p.SelectedItems).Select(item => item.Id)
                                                   .Concat(treeViewModel.Projects.Values.Where(p => p.IsSelected).Select(p => p.Id)));

            foreach (var kvPair in treeViewModel.Projects)
            {
                var list = new ProjectDataJsonVersioned
                {
                    Id = kvPair.Value.Id,
                    ExclusiveMode = kvPair.Value.ExclusiveMode,
                    Delimiter = kvPair.Value.Delimiter,
                    Postfix = kvPair.Value.Postfix,
                    Prefix = kvPair.Value.Prefix,
                    Items = TransformCmdList(kvPair.Value.Items),

                    // not in JSON
                    Expanded = kvPair.Value.IsExpanded,
                    Selected = kvPair.Value.IsSelected,
                };
                data.ProjectArguments.Add(kvPair.Key, list);
            }

            return data;
        }
    }
}
