using System;
using System.Collections;
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
    public class TreeViewModel : PropertyChangedBase
    {
        private ObservableCollectionEx<CmdProject> projects;
        private ObservableCollectionEx<CmdProject> startupProjects;
        private object treeitems;
        private bool showAllProjects;

        public ObservableCollectionEx<CmdProject> Projects => projects;

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

        public CmdProject FocusedProject => projects.FirstOrDefault(project => project.IsFocusedProject) ?? startupProjects.FirstOrDefault();

        public ObservableCollectionEx<CmdProject> StartupProjects => startupProjects;

        public IDropTarget DropHandler { get; private set; }
        public IDragSource DragHandler { get; private set; }

        public List<TreeViewItemEx> DragedTreeViewItems { get; }

        public TreeViewModel()
        {
            DropHandler = new DropHandler(this);
            DragHandler = new DragHandler(this);

            projects = new ObservableCollectionEx<CmdProject>();
            projects.CollectionChanged += OnProjectsChanged;
            treeitems = null;

            startupProjects = new ObservableCollectionEx<CmdProject>();
            startupProjects.CollectionChanged += OnStartupProjectsChanged;

            DragedTreeViewItems = new List<TreeViewItemEx>();
        }

        private void OnProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in startupProjects.Except(projects).ToList())
                {
                    startupProjects.Remove(cmdProject);
                }
            }
            else
            {
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<CmdProject>())
                    {
                        startupProjects.Remove(item);
                    }
                }
            }
        }

        private void OnStartupProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in projects)
                {
                    cmdProject.IsStartupProject = false;
                }

                foreach (var item in startupProjects)
                {
                    item.IsStartupProject = true;
                }
            }
            else
            {
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
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
        
        public event EventHandler<IList> SelectedItemsChanged;
    }
}
