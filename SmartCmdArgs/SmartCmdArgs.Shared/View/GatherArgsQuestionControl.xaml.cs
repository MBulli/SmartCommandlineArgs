using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SmartCmdArgs.View
{
    public sealed partial class GatherArgsQuestionControl : UserControl
    {
        public GatherArgsQuestionControl()
        {
            this.InitializeComponent();
        }

        private void RememberCount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            DoClose(true);
        }

        private void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            DoClose(false);
        }

        private void DoClose(bool result)
        {
            GatherArgsQuestionDialog parentWindow = Window.GetWindow(this) as GatherArgsQuestionDialog;
            parentWindow.DoClose(result, RememberCheckbox.IsChecked ?? false);
        }
    }
}
