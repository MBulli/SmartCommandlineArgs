using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SmartCmdArgs.ViewModel
{
    static class DataObjectGenerator
    {
        public static DataObject Genrate(IEnumerable<CmdBase> data)
        {
            var dataObject = new DataObject();

            if (data == null)
                return dataObject;

            var dataList = data.ToList();

            dataObject.SetData(CmdArgsPackage.DataObjectCmdListFormat, dataList);
            dataObject.SetData(CmdArgsPackage.DataObjectCmdJsonFormat, SerializeToJson(dataList));
            dataObject.SetText(string.Join(Environment.NewLine, SerializeToStringList(dataList)));

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
                if (cmd is CmdArgument arg)
                {
                    yield return indent + arg.Value;
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
                   || dataObject.GetDataPresent(DataFormats.Text);
        }

        public static IEnumerable<CmdBase> Extract(IDataObject dataObject)
        {
            if (dataObject == null)
                return null;

            IEnumerable<CmdBase> result = null;
            if (dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdListFormat))
                result = dataObject.GetData(CmdArgsPackage.DataObjectCmdListFormat) as List<CmdBase>;
            if (result == null && dataObject.GetDataPresent(CmdArgsPackage.DataObjectCmdJsonFormat))
                result = DeserializeFromJson(dataObject.GetData(CmdArgsPackage.DataObjectCmdJsonFormat) as string);
            if (result == null && dataObject.GetDataPresent(DataFormats.Text))
                result = DeserializeFromStringList((dataObject.GetData(DataFormats.Text) as string)?.Split(new []{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries));
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
            Stack<CmdGroup> groupStack = new Stack<CmdGroup>();
            CmdGroup rootGroup = new CmdGroup(null);
            groupStack.Push(rootGroup);
            foreach (var item in strings)
            {
                var readLevel = item.TakeWhile(c => c == '\t').Count();

                while (readLevel < groupStack.Count-1)
                    groupStack.Pop();

                if (item.EndsWith(":"))
                    groupStack.Push(new CmdGroup(item));
                else
                    groupStack.Peek().Items.Add(new CmdArgument(item));
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
            public IEnumerable<DataObjectJsonItem> Items { get; set; } = null;


            public static IEnumerable<DataObjectJsonItem> Convert(IEnumerable<CmdBase> data)
            {
                foreach (var cmd in data)
                {
                    if (cmd is CmdArgument arg)
                    {
                        yield return new DataObjectJsonItem {Enabled = arg.IsChecked, Value = arg.Value};
                    }
                    else if (cmd is CmdGroup grp)
                    {
                        yield return new DataObjectJsonItem {Value = grp.Value, Items = Convert(grp.Items)};
                    }
                }
            }

            public static IEnumerable<CmdBase> Convert(IEnumerable<DataObjectJsonItem> data)
            {
                foreach (var item in data)
                {
                    if (item.Items == null)
                    {
                        yield return new CmdArgument(item.Value, item.Enabled == true);
                    }
                    else
                    {
                        yield return new CmdGroup(item.Value, Convert(item.Items));
                    }
                }
            }
        }
    }
}
