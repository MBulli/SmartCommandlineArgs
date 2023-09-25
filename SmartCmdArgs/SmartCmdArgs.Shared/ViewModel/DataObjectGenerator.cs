using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SmartCmdArgs.ViewModel
{
    static class DataObjectGenerator
    {
        public static DataObject Generate(IEnumerable<CmdBase> data, bool includeObject)
        {
            var dataObject = new DataObject();

            if (data == null)
                return dataObject;

            var dataList = data.ToList();

            if (includeObject)
                dataObject.SetData(CmdArgsPackage.DataObjectCmdListFormat, dataList);

            dataObject.SetData(CmdArgsPackage.DataObjectCmdJsonFormat, SerializeToJson(dataList));
            dataObject.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, SerializeToStringList(dataList)));

            return dataObject;
        }

        private static string SerializeToJson(IEnumerable<CmdBase> data)
        {
            var jsonData = DataObjectJsonItem.Convert(data);
            if (jsonData == null)
                return null;

            return JsonConvert.SerializeObject(jsonData);
        }

        private static IEnumerable<String> SerializeToStringList(IEnumerable<CmdBase> data, int level = 0)
        {
            string indent = new String('\t', level);
            foreach (var cmd in data)
            {
                if (cmd is CmdParameter param)
                {
                    yield return indent + param.Value;
                }
                else if (cmd is CmdGroup grp)
                {
                    yield return indent + grp.Value + ":";
                    foreach (var line in SerializeToStringList(grp.Items, level+1))
                    {
                        yield return line;
                    }
                }
            }
        }

        public static bool ExtractableDataPresent(IDataObject dataObject)
        {
            return dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdListFormat)
                   || dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdJsonFormat)
                   || dataObject.GetDataPresent(DataFormats.Text)
                   || dataObject.GetDataPresent(DataFormats.FileDrop);
        }

        public static IEnumerable<CmdBase> Extract(IDataObject dataObject, bool includeObject)
        {
            if (dataObject == null)
                return null;

            IEnumerable<CmdBase> result = null;
            if (includeObject && dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdListFormat))
                result = dataObject.GetData(CmdArgsPackage.DataObjectCmdListFormat) as List<CmdBase>;
            if (result == null && dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdJsonFormat))
                result = DeserializeFromJson(dataObject.GetData(CmdArgsPackage.DataObjectCmdJsonFormat) as string);
            if (result == null && dataObject.GetDataPresent(DataFormats.UnicodeText))
                result = DeserializeFromStringList((dataObject.GetData(DataFormats.UnicodeText) as string)?.Split(new []{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries));
            if (result == null && dataObject.GetDataPresent(DataFormats.FileDrop))
                result = DeserializeFromStringList((dataObject.GetData(DataFormats.FileDrop) as string[])?.Select(s => $"\"{s}\""));
            return result;
        }

        private static IEnumerable<CmdBase> DeserializeFromJson(string jsonSting)
        {
            if (jsonSting == null)
                return null;
            var jsonData = JsonConvert.DeserializeObject<List<DataObjectJsonItem>>(jsonSting);
            if (jsonData == null)
                return null;

            return DataObjectJsonItem.Convert(jsonData);
        }

        private static IEnumerable<CmdBase> DeserializeFromStringList(IEnumerable<string> strings)
        {
            if (strings == null)
                return null;

            Stack<CmdGroup> groupStack = new Stack<CmdGroup>();
            CmdGroup rootGroup = new CmdGroup(null);
            groupStack.Push(rootGroup);
            foreach (var item in strings)
            {
                var level = Math.Min(groupStack.Count-1, item.TakeWhile(c => c == '\t').Count());

                while (level < groupStack.Count-1)
                    groupStack.Pop();

                var trimmedItem = item.Substring(level);

                if (trimmedItem.EndsWith(":"))
                {
                    var group = new CmdGroup(trimmedItem.Substring(0, trimmedItem.Length-1));
                    groupStack.Peek().Add(group);
                    groupStack.Push(group);
                }
                else
                    groupStack.Peek().Add(new CmdParameter(CmdParamType.CmdArg, trimmedItem));
            }
            return rootGroup.Items;
        }

        private class DataObjectJsonItem
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool? Enabled { get; set; } = null;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Value { get; set; } = null;
            
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string ProjectConfig { get; set; } = null;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string ProjectPlatform { get; set; } = null;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string LaunchProfile { get; set; } = null;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool ExclusiveMode { get; set; } = false;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate), DefaultValue(" ")]
            public string Delimiter { get; set; } = " ";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore), DefaultValue("")]
            public string Prefix { get; set; } = "";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, NullValueHandling = NullValueHandling.Ignore), DefaultValue("")]
            public string Postfix { get; set; } = "";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool DefaultChecked { get; set; } = false;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate), DefaultValue(CmdParamType.CmdArg)]
            public CmdParamType Type { get; set; } = CmdParamType.CmdArg;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<DataObjectJsonItem> Items { get; set; } = null;


            public static IEnumerable<DataObjectJsonItem> Convert(IEnumerable<CmdBase> data)
            {
                foreach (var cmd in data)
                {
                    if (cmd is CmdParameter param)
                    {
                        yield return new DataObjectJsonItem {
                            Type = param.ParamType,
                            Enabled = param.IsChecked,
                            Value = param.Value,
                            DefaultChecked = param.DefaultChecked
                        };
                    }
                    else if (cmd is CmdGroup grp)
                    {
                        yield return new DataObjectJsonItem {
                            Value = grp.Value,
                            ProjectConfig = grp.ProjectConfig,
                            ProjectPlatform = grp.ProjectPlatform,
                            LaunchProfile = grp.LaunchProfile,
                            ExclusiveMode = grp.ExclusiveMode,
                            Delimiter = grp.Delimiter,
                            Prefix = grp.Prefix,
                            Postfix = grp.Postfix,
                            Items = Convert(grp.Items)
                        };
                    }
                }
            }

            public static IEnumerable<CmdBase> Convert(IEnumerable<DataObjectJsonItem> data)
            {
                foreach (var item in data)
                {
                    if (item.Items == null)
                    {
                        yield return new CmdParameter(
                            item.Type,
                            item.Value,
                            item.Enabled ?? false,
                            item.DefaultChecked);
                    }
                    else
                    {
                        yield return new CmdGroup(
                            item.Value,
                            Convert(item.Items),
                            exclusiveMode: item.ExclusiveMode,
                            projConf: item.ProjectConfig,
                            projPlatform: item.ProjectPlatform,
                            launchProfile: item.LaunchProfile,
                            delimiter: item.Delimiter,
                            postfix: item.Postfix,
                            prefix: item.Prefix);
                    }
                }
            }
        }
    }
}
