using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ListViewModel lvm = new ListViewModel();

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = lvm;

            CmdProject prj = new CmdProject("Project1");

            prj.Items.Add(new CmdArgument("Hello"));
            prj.Items.Add(new CmdGroup("Grp", false, 
                new[] {
                    new CmdArgument("Welt"),
                    new CmdArgument("Mond")
            }));
            lvm.Projects.Add(prj);


            CmdProject prj2 = new CmdProject("Project2");

            prj2.Items.Add(new CmdArgument("Hello2"));
            prj2.Items.Add(new CmdGroup("G2rp", false,
                new CmdBase[]
                {
                    new CmdArgument("Wel2t"),
                    new CmdArgument("Mond2"),
                    new CmdGroup("Gr3p", false,
                        new[]
                        {
                            new CmdArgument("Wel3t"),
                            new CmdArgument("Mond3")
                        })
                }));
            lvm.Projects.Add(prj2);


            lvm.ShowAllProjects = false;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            lvm.ShowAllProjects = ((CheckBox)sender).IsChecked.Value;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            lvm.ShowAllProjects = ((CheckBox)sender).IsChecked.Value;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!lvm.StartupProjects.Contains(lvm.Projects[0]))
                lvm.StartupProjects.Add(lvm.Projects[0]);
            else
                lvm.StartupProjects.Remove(lvm.Projects[0]);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (!lvm.StartupProjects.Contains(lvm.Projects[1]))
                lvm.StartupProjects.Add(lvm.Projects[1]);
            else
                lvm.StartupProjects.Remove(lvm.Projects[1]);
        }

        int counter = 0;
        private void Button_AddPrjClick(object sender, RoutedEventArgs e)
        {
            lvm.Projects.Add(new CmdProject($"new project {++counter}"));
        }
        
        private void Button_AddSprjClick(object sender, RoutedEventArgs e)
        {
            var prj = new CmdProject($"new project {++counter}");
            lvm.Projects.Add(prj);
            lvm.StartupProjects.Add(prj);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            lvm.Projects[0].Items[0].IsSelected = true;
        }
    }
}
