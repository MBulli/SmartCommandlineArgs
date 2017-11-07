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
        public MainWindow()
        {
            InitializeComponent();

            var lvm = new ListViewModel();
            this.DataContext = lvm;

            CmdProject prj = new CmdProject() { Name = "Project1" };
            prj.Items = new ObservableCollection<CmdItem>();

            prj.Items.Add(new CmdArgument() { Command = "Hello" });
            prj.Items.Add(new CmdGroup()
            {
                Name = "Grp",
                Items = new ObservableCollection<CmdItem>(new[] {
                    new CmdArgument() { Command = "Welt" },
                    new CmdArgument() { Command = "Mond" }
            })});
            lvm.Projects.Add(prj);


            prj = new CmdProject() { Name = "Project2" };
            prj.Items = new ObservableCollection<CmdItem>();

            prj.Items.Add(new CmdArgument() { Command = "Hello2" });
            prj.Items.Add(new CmdGroup()
            {
                Name = "G2rp",
                Items = new ObservableCollection<CmdItem>(new CmdItem[] {
                    new CmdArgument() { Command = "Wel2t" },
                    new CmdArgument() { Command = "Mond2" },
                    new CmdGroup()
                    {
                      Name = "Gr3p",
                      Items = new ObservableCollection<CmdItem>(new[] {
                        new CmdArgument() { Command = "Wel3t" },
                        new CmdArgument() { Command = "Mond3" }
                      })
                    }
            })
            });
            lvm.Projects.Add(prj);

            
        }
    }
}
