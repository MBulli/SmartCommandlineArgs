using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using SmartCmdArgs.ViewModel;
using SmartCmdArgs.Helper;
using System.IO;
using System.Windows.Interop;

namespace SmartCmdArgs.View
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsControl()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                Background = Brushes.White;
            }

        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow.DialogResult = false;
            parentWindow.Close();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow.DialogResult = true;
            parentWindow.Close();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var curPath = ViewModel?.JsonRootPath;

            if (string.IsNullOrWhiteSpace(curPath))
                curPath = ".";

            curPath = CmdArgsPackage.Instance.MakePathAbsoluteBasedOnSolutionDir(curPath);

            var window = TreeHelper.FindAncestorOrSelf<Window>(this);

            var dialog = new FolderSelectDialog();
            dialog.InitialDirectory = curPath;
            if (dialog.Show(new WindowInteropHelper(window).Handle))
            {
                SettingsViewModel settings = this.DataContext as SettingsViewModel;
                settings.JsonRootPath = CmdArgsPackage.Instance.MakePathRelativeBasedOnSolutionDir(dialog.FileName);
            }
        }

        private string GetFullCustomRootPath()
        {
            return CmdArgsPackage.Instance.MakePathAbsoluteBasedOnSolutionDir(ViewModel?.JsonRootPath);
        }

        private void CustomRootPathChanged(object sender, RoutedEventArgs e)
        {
            FullCustomRootPathLink.Inlines.Clear();
            FullCustomRootPathInvalid.Text = null;

            var path = GetFullCustomRootPath();

            if (!string.IsNullOrWhiteSpace(path))
                FullCustomRootPathLink.Inlines.Add(path);
            else
                FullCustomRootPathInvalid.Text = "<invalid path>";
        }

        private void FullCustomRootPathClicked(object sender, RoutedEventArgs e)
        {
            var directoryName = GetFullCustomRootPath();
            if (Directory.Exists(directoryName))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{directoryName}\"");
            }
        }
    }
}
