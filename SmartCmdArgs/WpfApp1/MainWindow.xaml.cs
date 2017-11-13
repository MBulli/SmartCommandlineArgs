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

            CmdProject prj = new CmdProject("Project1");

            prj.Items.Add(new CmdArgument("Hello"));
            prj.Items.Add(new CmdGroup("Grp", false, 
                new[] {
                    new CmdArgument("Welt"),
                    new CmdArgument("Mond")
            }));
            lvm.Projects.Add(prj);


            prj = new CmdProject("Project2");

            prj.Items.Add(new CmdArgument("Hello2"));
            prj.Items.Add(new CmdGroup("G2rp", false,
                new ICmdItem[]
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
            lvm.Projects.Add(prj);
        }
    }
}
