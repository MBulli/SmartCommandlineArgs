using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SmartCmdArgs.View.UserControls
{
    public partial class SettingsCheckBox : UserControl
    {
        public SettingsCheckBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(SettingsCheckBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool? IsChecked
        {
            get { return (bool?)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register(nameof(LabelText), typeof(string), typeof(SettingsCheckBox));

        public string LabelText
        {
            get { return (string)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(bool?), typeof(SettingsCheckBox));

        public bool? DefaultValue
        {
            get { return (bool?)GetValue(DefaultValueProperty); }
            set { SetValue(DefaultValueProperty, value); }
        }

        public static readonly DependencyProperty RequiredValueProperty =
            DependencyProperty.Register(nameof(RequiredValue), typeof(bool?), typeof(SettingsCheckBox));

        public bool? RequiredValue
        {
            get { return (bool?)GetValue(RequiredValueProperty); }
            set { SetValue(RequiredValueProperty, value); }
        }

        public string Description { 
            get => DescriptionTextBlock.Text;
            set
            {
                DescriptionTextBlock.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
                DescriptionTextBlock.Text = value;
            }
        }

        public string RequiredDisplayName { get => RequiredDisplayNameRun.Text; set => RequiredDisplayNameRun.Text = value; }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == DefaultValueProperty)
            {
                var bindingExpression = GetBindingExpression(DefaultValueProperty);
                MainCheckBox.IsThreeState = bindingExpression != null;
            }
        }
    }
}
