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

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for ArgumentItemView.xaml
    /// </summary>
    public partial class ArgumentItemView : UserControl
    {
        private bool IsCoolEdit = false;

        public bool IsEditing
        {
            get { return (bool)GetValue(IsEditingProperty); }
            set { SetValue(IsEditingProperty, value); }
        }

        public string Command
        {
            get { return (string)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }


        public ArgumentItemView()
        {
            InitializeComponent();
        }

        private void StartEdit(bool resetValue = false)
        {
            textbox.Text = resetValue ? string.Empty : Command;

            textbox.SelectAll();
            IsCoolEdit = true;
        }

        private void CommitEdit()
        {
            Command = textbox.Text;

            IsCoolEdit = false;
            IsEditing = false;
        }

        private void CancelEdit()
        {
            IsCoolEdit = false;
            IsEditing = false;
        }

        private void OnCommandChanged(DependencyPropertyChangedEventArgs e)
        {
            textblock.Text = (string)e.NewValue;
            textbox.Text = (string)e.NewValue;
        }

        protected void OnIsEditingChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                textblock.Visibility = Visibility.Collapsed;
                textbox.Visibility = Visibility.Visible;
                textbox.Focus();
            }
            else
            {
                textblock.Visibility = Visibility.Visible;
                textbox.Visibility = Visibility.Collapsed;
            }
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
                if (IsCoolEdit)
                    CommitEdit();
                else
                    StartEdit(resetValue: false);

                e.Handled = true;
            }
            else if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                if (!IsCoolEdit)
                {
                    StartEdit(resetValue: true);
                    e.Handled = false;
                }
            }
        }

        private static void OnIsEditingDepPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ArgumentItemView)d).OnIsEditingChanged(e);
        }

        private static void OnCommandDepPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ArgumentItemView)d).OnCommandChanged(e);
        }

        public static readonly DependencyProperty IsEditingProperty =
                DependencyProperty.Register(nameof(IsEditing), typeof(bool), typeof(ArgumentItemView),
                    new PropertyMetadata(false, OnIsEditingDepPropertyChanged ));


        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(string), typeof(ArgumentItemView),
                new PropertyMetadata(null, OnCommandDepPropertyChanged));

    }
}
