using System.Windows;
using System.Windows.Controls;

namespace SmartCmdArgs.View
{
    public sealed partial class SetCustomDelimiterControl : UserControl
    {
        public SetCustomDelimiterControl()
        {
            this.InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DelimiterTextBox.Focus();
            DelimiterTextBox.SelectAll();
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
    }
}
