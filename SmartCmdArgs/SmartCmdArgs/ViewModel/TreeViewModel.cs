using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GongSolutions.Wpf.DragDrop;

using SmartCmdArgs.Helper;
using SmartCmdArgs.View;

namespace SmartCmdArgs.ViewModel
{
    class TreeViewModel : PropertyChangedBase
    {
        private ObservableCollection<CmdProject> projects;
        private ObservableCollection<CmdProject> startupProjects;
        private object treeitems;
        private bool showAllProjects;

        public ObservableCollection<CmdProject> Projects { get => projects; }
        public object TreeItems
        {
            get => treeitems;
            private set => SetAndNotify(value, ref treeitems);
        }

        public bool ShowAllProjects
        {
            get => showAllProjects;
            set
            {
                if (showAllProjects != value)
                {
                    SetAndNotify(value, ref showAllProjects);
                    UpdateTree();
                }
            }
        }
        public ObservableCollection<CmdProject> StartupProjects { get => startupProjects; }

        public IDropTarget DropHandler { get; private set; }
        public IDragSource DragHandler { get; private set; }

        public List<TreeViewItemEx> DragedTreeViewItems { get; }

        public TreeViewModel()
        {
            DropHandler = new DropHandler(this);
            DragHandler = new DragHandler(this);

            projects = new ObservableCollection<CmdProject>();
            treeitems = null;

            startupProjects = new ObservableCollection<CmdProject>();
            startupProjects.CollectionChanged += OnStartupProjectsChanged;

            DragedTreeViewItems = new List<TreeViewItemEx>();
        }

        private void OnStartupProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove
                || e.Action == NotifyCollectionChangedAction.Replace
                || e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in e.OldItems.Cast<CmdProject>())
                {
                    item.IsStartupProject = false;
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var item in e.NewItems.Cast<CmdProject>())
                {
                    item.IsStartupProject = true;
                }
            }

            UpdateTree();
        }

        private void UpdateTree()
        {
            if (ShowAllProjects)
            {
                TreeItems = projects;
            }
            else
            {
                if (startupProjects.Count == 1)
                {
                    TreeItems = startupProjects[0].Items;
                }
                else
                {
                    // Fixes a strange potential bug in WPF. If ToList() is missing the project will be shown twice.
                    TreeItems = startupProjects.ToList();
                }
            }
        }
    }
}
