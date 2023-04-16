using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SmartCmdArgs.View.UserControls
{
    public partial class SettingsCheckBox : UserControl
    {
        public SettingsCheckBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool?), typeof(SettingsCheckBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool? IsChecked
        {
            get { return (bool?)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register("LabelText", typeof(string), typeof(SettingsCheckBox));

        public string LabelText
        {
            get { return (string)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register("DefaultValue", typeof(string), typeof(SettingsCheckBox));

        public string DefaultValue
        {
            get { return (string)GetValue(DefaultValueProperty); }
            set { SetValue(DefaultValueProperty, value); }
        }
    }
}
