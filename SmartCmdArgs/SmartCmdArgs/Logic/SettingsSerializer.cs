using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Logic
{
    public class SettingsSerializer
    {
        public static string Serialize(SettingsViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            var data = new SettingsJson
            {
                VcsSupportEnabled = vm.VcsSupportEnabled,
                UseSolutionDir = vm.UseSolutionDir,
                MacroEvaluationEnabled = vm.MacroEvaluationEnabled,
                ShowDialogIfNoConfig = vm.ShowDialogIfNoConfig
            };

            if (data.IsDefault())
                return null;

            return JsonConvert.SerializeObject(data, Formatting.Indented);
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
