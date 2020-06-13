using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.ViewModel;
using Microsoft.VisualStudio.PlatformUI;

namespace SmartCmdArgs.View
{
    class SettingsDialog : DialogWindow
    {
        private SettingsControl _settingsControl;

        public SettingsDialog(SettingsViewModel settingsViewModel) : base()
        {
            MinWidth = 600;
            MinHeight = 350;

            Width = 610;
            Height = 370;

            _settingsControl = new SettingsControl();
            _settingsControl.DataContext = settingsViewModel;

            Content = _settingsControl;
        }
    }
}
