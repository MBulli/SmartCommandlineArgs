using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartCmdArgs.View
{
    /// <summary>
    /// Interaktionslogik für AutocompletePopup.xaml
    /// </summary>
    public partial class AutocompletePopup : Popup
    {
        public IList ChoicesItemsSource
        {
            get { return (IList)GetValue(ChoicesItemsSourceProperty); }
            set { SetValue(ChoicesItemsSourceProperty, value); }
        }

        public AutocompletePopup()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ChoicesItemsSourceProperty =
            DependencyProperty.Register("ChoicesItemsSource", typeof(IList), typeof(AutocompletePopup), new PropertyMetadata(null));
    }
}
