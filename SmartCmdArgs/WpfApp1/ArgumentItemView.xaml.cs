using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for ArgumentItemView.xaml
    /// </summary>
    public partial class ArgumentItemView : UserControl
    {
        private bool _isInEditMode;

        public string EditingArgument
        {
            get => textbox.Text;
            set => textbox.Text = value;
        }

        public string Argument
        {
            get => (string)GetValue(ArgumentProperty);
            set => SetValue(ArgumentProperty, value);
        }
        
        public string IsChecked
        {
            get => (string)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public ArgumentItemView()
        {
            InitializeComponent();
        }

        public bool IsInEditMode => _isInEditMode;

        public void BeginEdit(bool resetValue)
        {
            if (!IsInEditMode)
            {
                EditingArgument = resetValue ? "" : Argument;
                EnterEditMode();
            }
        }

        public void CancelEdit()
        {
            if (IsInEditMode)
            {
                LeaveEditMode();
            }
        }

        public void CommitEdit()
        {
            if (IsInEditMode)
            {
                Argument = EditingArgument;
                LeaveEditMode();
            }
        }

        private void EnterEditMode()
        {
            textblock.Visibility = Visibility.Collapsed;
            textbox.Visibility = Visibility.Visible;
            textbox.SelectAll();
            textbox.Focus();
            _isInEditMode = true;
        }

        private void LeaveEditMode()
        {
            _isInEditMode = false;
            textblock.Visibility = Visibility.Visible;
            textbox.Visibility = Visibility.Collapsed;
        }
   
        private void textbox_KeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($" V: KEY DOWN {e.Key}");
            if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Return || e.Key == Key.F2)
            {
                if (IsInEditMode)
                    CommitEdit();
                else
                    BeginEdit(resetValue: false);

                e.Handled = true;
            }
            else if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                if (!IsInEditMode)
                {
                    BeginEdit(resetValue: true);
                    e.Handled = false;
                }
            }
        }

        protected virtual void OnArgumentProperyChanged(DependencyPropertyChangedEventArgs e)
        {
            textblock.Text = (string) e.NewValue;
        }

        private static void OnArgumentDepPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ArgumentItemView)d).OnArgumentProperyChanged(e);
        }
        
        public static readonly DependencyProperty ArgumentProperty =
            DependencyProperty.Register(nameof(Argument), typeof(string), typeof(ArgumentItemView),
                new PropertyMetadata(null, OnArgumentDepPropertyChanged));


        protected virtual void OnIsCheckedProperyChanged(DependencyPropertyChangedEventArgs e)
        {
            checkbox.IsChecked = (bool)e.NewValue;
        }

        private static void OnIsCheckedDepPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ArgumentItemView)d).OnIsCheckedProperyChanged(e);
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ArgumentItemView),
                new PropertyMetadata(false, OnIsCheckedDepPropertyChanged));
    }
}
