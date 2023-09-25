using SmartCmdArgs.Helper;
using System;
using System.Collections.Generic;

namespace SmartCmdArgs.ViewModel
{
    public class CmdProject : CmdContainer
    {
        private TreeViewModel _parentTreeViewModel;
        public TreeViewModel ParentTreeViewModel
        {
            get => _parentTreeViewModel;
            set
            {
                if (value == null)
                    GetEnumerable(includeSelf: true).ForEach(i => i.IsSelected = false);
                _parentTreeViewModel = value;
            }
        }

        private bool isStartupProject = false;
        public bool IsStartupProject
        {
            get => isStartupProject; set
            {
                // this is taken care of by the code who sets 'IsStartupProject' to increase performance
                //if (value != isStartupProject)
                //    ParentTreeViewModel.UpdateTree();

                SetAndNotify(value, ref isStartupProject);
            }
        }

        public override bool IsSelected
        {
            get => isSelected;
            set
            {
                SetAndNotify(value, ref isSelected);
                ParentTreeViewModel?.OnItemSelectionChanged(this);
            }
        }

        protected override Predicate<CmdBase> FilterPredicate => Filter;
        private Predicate<CmdBase> filter;
        public Predicate<CmdBase> Filter
        {
            get => filter; set { filter = value; RefreshFilters(); }
        }

        public override Guid ProjectGuid => Id;

        public Guid Kind { get; set; }

        public CmdProject(Guid id, Guid kind, string displayName, IEnumerable<CmdBase> items, bool isExpanded, bool exclusiveMode, string delimiter, string prefix, string postfix)
            : base(id, displayName, items, isExpanded, exclusiveMode, delimiter, prefix, postfix)
        {
            Kind = kind;
        }

        public override CmdBase Copy()
        {
            throw new InvalidOperationException("Can't copy a project");
        }

        protected override void BubbleEvent(TreeEventBase treeEvent, CmdBase receiver)
        {
            if (treeEvent != null)
                treeEvent.AffectedProject = this;

            ParentTreeViewModel?.OnTreeEvent(treeEvent);
        }
    }
}
