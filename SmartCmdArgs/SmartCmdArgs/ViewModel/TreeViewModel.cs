using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

using GongSolutions.Wpf.DragDrop;
using SmartCmdArgs.Helper;
using SmartCmdArgs.View;

namespace SmartCmdArgs.ViewModel
{
    public class TreeViewModel : PropertyChangedBase
    {
        private ObservableDictionary<string, CmdProject> projects;
        private ObservableCollection<CmdProject> startupProjects;
        private object treeitems;
        private bool showAllProjects;

        public ObservableDictionary<string, CmdProject> Projects => projects;

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

        public CmdProject FocusedProject => projects.Values.FirstOrDefault(project => project.IsFocusedProject) ?? startupProjects.FirstOrDefault();

        public ObservableCollection<CmdProject> StartupProjects => startupProjects;

        public IDropTarget DropHandler { get; private set; }
        public IDragSource DragHandler { get; private set; }

        public List<TreeViewItemEx> DragedTreeViewItems { get; }

        public TreeViewModel()
        {
            DropHandler = new DropHandler(this);
            DragHandler = new DragHandler(this);

            projects = new ObservableDictionary<string, CmdProject>();
            projects.CollectionChanged += OnProjectsChanged;
            treeitems = null;

            startupProjects = new ObservableCollection<CmdProject>();
            startupProjects.CollectionChanged += OnStartupProjectsChanged;

            DragedTreeViewItems = new List<TreeViewItemEx>();
        }

        private void OnProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in startupProjects.Except(projects.Values).ToList())
                {
                    startupProjects.Remove(cmdProject);
                }
            }
            else
            {
                var removedKeys = new HashSet<string>();
                if (e.Action == NotifyCollectionChangedAction.Remove
                    || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<KeyValuePair<string, CmdProject>>())
                    {
                        if (startupProjects.Remove(item.Value) && e.Action == NotifyCollectionChangedAction.Replace)
                            removedKeys.Add(item.Key);
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.NewItems.Cast<KeyValuePair<string, CmdProject>>())
                    {
                        if (removedKeys.Contains(item.Key))
                            startupProjects.Add(item.Value);
                    }
                }
            }
        }

        private void OnStartupProjectsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var cmdProject in projects.Values)
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
                    TreeItems = startupProjects.First().Items;
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
