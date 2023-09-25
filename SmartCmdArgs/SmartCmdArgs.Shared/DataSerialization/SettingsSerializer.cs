using Newtonsoft.Json;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;

namespace SmartCmdArgs.DataSerialization
{
    public class SettingsSerializer
    {
        public static string Serialize(SettingsViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            var data = new SettingsJson(vm);

            return JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
        }

        public static SettingsJson Deserialize(string jsonStr)
        {
            Logger.Info($"Try to parse settings json: '{jsonStr}'");

            if (string.IsNullOrEmpty(jsonStr))
            {
                // If the file is empty return empty solution data
                Logger.Info("Got empty settings json string. Returning default SettingsJson");
                return new SettingsJson();
            }

            return JsonConvert.DeserializeObject<SettingsJson>(jsonStr);
        }
    }
}
