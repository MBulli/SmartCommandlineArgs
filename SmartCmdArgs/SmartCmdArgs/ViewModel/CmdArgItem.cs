using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs.ViewModel
{
    public class CmdBase : PropertyChangedBase
    {
        public Guid Id { get; }

        private CmdContainer parent;
        public CmdContainer Parent { get => parent; set => OnParentChanged(parent, value); }

        private string value;
        public string Value { get => value; set => OnValueChanged(this.value, value); }

        protected bool? isChecked;
        public bool? IsChecked
        {
            get => isChecked;
            set
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                    ExclusiveChecked();
                else
                    OnIsCheckedChanged(isChecked, value, true);
            }
        }

        protected bool isSelected;
        public virtual bool IsSelected
        {
            get => isSelected;
            set { OnIsSelectedChanged(isSelected, value); }
        }

        public bool IsFocusedItem { get; set; }

        public virtual bool IsEditable => false;

        public CmdBase(Guid id, string value, bool? isChecked = false)
        {
            if (id == Guid.Empty)
                id = Guid.NewGuid();
            Id = id;
            this.value = value;
            this.isChecked = isChecked;
        }

        /// <summary>
        /// Notifies the parent that an event occurred.
        /// The event will eventually reach the CmdProject root element
        /// </summary>
        protected virtual void BubbleEvent(TreeEventBase treeEvent, CmdBase receiver = null)
        {
            if (receiver != null)
            {
                receiver.BubbleEvent(treeEvent);
            }
            else
            {
                this.Parent?.BubbleEvent(treeEvent);
            }
        }

        protected virtual void OnParentChanged(CmdContainer oldParent, CmdContainer newParent)
        {
            SetAndNotify(newParent, ref this.parent, nameof(Parent));

            if (oldParent != newParent)
            {
                // Use oldParent if newParent is null as we've been removed from the tree
                // but still want to notify everbody that we were removed.
                var receiver = newParent ?? oldParent;
                BubbleEvent(new ParentChangedEvent(this, oldParent, newParent), receiver);
            }
        }

        protected virtual void OnValueChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref this.value, nameof(Value));

            if (oldValue != newValue)
            { 
                BubbleEvent(new ValueChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnIsSelectedChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref isSelected, nameof(IsSelected));

            if (oldValue != newValue)
            {
                BubbleEvent(new SelectionChangedEvent(this, oldValue, newValue));
            }           
        }

        protected virtual void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            SetAndNotify(newValue, ref this.isChecked, nameof(IsChecked));

            if (notifyParent)
            {
                parent?.OnChildIsCheckedChanged(oldValue, newValue);
            }

            // TODO: Only bubble event if we're the origin.
            if (oldValue != newValue)
            {
                BubbleEvent(new CheckStateChangedEvent(this, oldValue, newValue));
            }
        }

        public void ToggleCheckedState()
        {
            if (IsChecked == null)
                IsChecked = false;
            else
                IsChecked = !IsChecked;
        }

        /// <summary>
        /// Check this item and uncheck every other.
        /// </summary>
        private void ExclusiveChecked()
        {
            var checkedItems = Parent.Items.Where(item => item.IsChecked != false).ToList();
            var checkState = checkedItems.Count != 1 || isChecked != true;

            foreach (var item in checkedItems)
            {
                item.OnIsCheckedChanged(item.IsChecked, newValue: false, notifyParent: false);
            }

            OnIsCheckedChanged(isChecked, checkState, notifyParent:true);
        }

        protected virtual void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {}

        public void SetIsCheckedWithoutNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, false);
        }

#region Editing
        private string editBackupValue;

        private bool isInEditMode;
        public bool IsInEditMode { get => isInEditMode; private set => SetAndNotify(value, ref this.isInEditMode); }

        public event EventHandler<EditModeChangedEventArgs> EditModeChanged;
        public enum EditMode
        {
            BeganEdit, BeganEditAndReset, CanceledEdit, CommitedEdit
        }

        public class EditModeChangedEventArgs : EventArgs
        {
            public EditMode Mode { get; }

            public EditModeChangedEventArgs(EditMode mode)
            {
                Mode = mode;
            }
        }

        public void BeginEdit(string initialValue = null)
        {
            ThrowIfNotEditable();

            if (!IsInEditMode)
            {
                editBackupValue = Value;
                if (initialValue != null)
                    Value = initialValue;

                IsInEditMode = true;
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(initialValue != null ? EditMode.BeganEditAndReset : EditMode.BeganEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: true));
            }
        }

        public void CancelEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                Value = editBackupValue;
                editBackupValue = null;

                IsInEditMode = false;
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(EditMode.CanceledEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: false));
            }
        }

        public void CommitEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                editBackupValue = null;

                IsInEditMode = false;
                EditModeChanged?.Invoke(this, new EditModeChangedEventArgs(EditMode.CommitedEdit));
                BubbleEvent(new ItemEditModeChangedEvent(this, isInEditMode: false));
            }
        }

        private void ThrowIfNotEditable()
        {
            if (!IsEditable)
                throw new InvalidOperationException("Can't execute edit operation on a not editable item!");
        }
#endregion

        public virtual CmdBase Copy()
        {
            return null;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}{{{Value}:{(IsChecked == null ? "▄" : (IsChecked.Value ? "☑" : "☐"))}:{(IsSelected ? "█" : "-")}}}";
        }
    }
    
    public class CmdContainer : CmdBase, IEnumerable<CmdBase>
    {
        protected bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                SetAndNotify(value, ref isExpanded);
                if (IsInEditMode)
                    CommitEdit();
                if (!isExpanded)
                    SetIsSelectedOnChildren(false);
            }
        }

        public ObservableRangeCollection<CmdBase> Items { get; }
        public ICollectionView ItemsView { get; }
        protected virtual Predicate<CmdBase> FilterPredicate => Parent?.FilterPredicate;

        public IEnumerable<CmdContainer> AllContainer => this.OfType<CmdContainer>();

        public IEnumerable<CmdArgument> AllArguments => this.OfType<CmdArgument>();

        public IEnumerable<CmdBase> SelectedItems => this.Where(item => item.IsSelected);

        public IEnumerable<CmdArgument> CheckedArguments => this.OfType<CmdArgument>().Where(arg => arg.IsChecked);

        public IEnumerable<CmdContainer> ExpandedContainer => this.OfType<CmdContainer>().Where(arg => arg.IsExpanded);

        public CmdContainer(Guid id, string value, IEnumerable<CmdBase> subItems = null, bool isExpanded = true)
            : base(id, value)
        {
            this.isExpanded = isExpanded;

            Items = new ObservableRangeCollection<CmdBase>();

            foreach (var item in subItems ?? Enumerable.Empty<CmdBase>())
            {
                Items.Add(item);
                item.Parent = this;
            }
            UpdateCheckedState();

            Items.CollectionChanged += ItemsOnCollectionChanged;

            ItemsView = CollectionViewSource.GetDefaultView(Items);

            ItemsView.Filter = o =>
            {
                if (FilterPredicate != null && o is CmdBase item)
                    return FilterPredicate(item);
                return true;
            };
        }

        public CmdContainer(string value, IEnumerable<CmdBase> items = null, bool isExpanded = true) 
            : this(Guid.NewGuid(), value, items, isExpanded)
        { }

        private void ItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in Items)
                {
                    item.Parent = this;
                }
            }
            else
            {
                if (e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.OldItems.Cast<CmdBase>())
                    {
                        item.Parent = null;
                    }
                }

                if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.NewItems.Cast<CmdBase>())
                    {
                        item.Parent = this;
                    }
                }
            }

            if (e.Action != NotifyCollectionChangedAction.Move)
            {
                UpdateCheckedState();
            }

            BubbleEvent(new ItemsChangedEvent(this, e));
        }

        protected void RefreshFilters()
        {
            Items.OfType<CmdContainer>().ForEach(container => container.RefreshFilters());
            ItemsView.Refresh();
        }

        public bool? UpdateCheckedState()
        {
            if (Items.Count == 0)
                base.OnIsCheckedChanged(IsChecked, false, true);
            else if (Items.All(item => item.IsChecked ?? false))
                base.OnIsCheckedChanged(IsChecked, true, true);
            else if (Items.All(item => !item.IsChecked ?? false))
                base.OnIsCheckedChanged(IsChecked, false, true);
            else
                base.OnIsCheckedChanged(IsChecked, null, true);

            return IsChecked;
        }

        protected override void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            base.OnIsCheckedChanged(oldValue, newValue, notifyParent);

            foreach (var item in Items)
            {
                item.SetIsCheckedWithoutNotifyingParent(newValue);
            }
        }

        protected override void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {
            if (newValue == true)
            {
                if (Items.All(item => item.IsChecked ?? false))
                    base.OnIsCheckedChanged(IsChecked, true, true);
                else
                    base.OnIsCheckedChanged(IsChecked, null, true);
            }
            else
            {
                if (Items.Any(item => item.IsChecked ?? true))
                    base.OnIsCheckedChanged(IsChecked, null, true);
                else
                    base.OnIsCheckedChanged(IsChecked, false, true);
            }
        }


        /// <summary>
        /// Sets the IsSelected property for every child to areSelected.
        /// </summary>
        /// <returns>True if any child changed its state.</returns>
        public bool SetIsSelectedOnChildren(bool areSelected)
        {
            bool result = false;
            foreach (var item in Items)
            {
                if (item.IsSelected != areSelected)
                {
                    item.IsSelected = areSelected;
                    result = true;
                }
                
                if (item is CmdContainer container)
                {
                    if (container.SetIsSelectedOnChildren(areSelected))
                        result = true;
                }
            }

            return result;
        }

        internal void MoveEntries(HashSet<CmdBase> items, int moveDirection)
        {
            var itemIndexList = Items.Where(items.Contains).Select(item => new KeyValuePair<CmdBase, int>(item, Items.IndexOf(item))).ToList();

            if (itemIndexList.Any()
                && (moveDirection == -1 && itemIndexList.Min(pair => pair.Value) > 0
                    || moveDirection == 1 && itemIndexList.Max(pair => pair.Value) < Items.Count - 1))
            {
                itemIndexList.Sort((pairA, pairB) => pairB.Value.CompareTo(pairA.Value) * moveDirection);

                foreach (var itemIndexPair in itemIndexList)
                {
                    Items.Move(itemIndexPair.Key, itemIndexPair.Value + moveDirection);
                }
            }

            Items.OfType<CmdContainer>().Where(item => !items.Contains(item)).ForEach(container => container.MoveEntries(items, moveDirection));
        }

        public IEnumerable<CmdBase> GetEnumerable(bool useView = false, bool includeSelf = false, bool includeCollapsed = true)
        {
            if (includeSelf)
                yield return this;

            if (IsExpanded || includeCollapsed)
            { 
                foreach (var item in useView ? ItemsView.Cast<CmdBase>() : Items)
                {
                    yield return item;
                    if (item is CmdContainer con && (con.IsExpanded || includeCollapsed))
                    {
                        foreach (var conItem in con.GetEnumerable(useView, false, includeCollapsed))
                            yield return conItem;
                    }
                }
            }
        }

        public IEnumerator<CmdBase> GetEnumerator()
        {
            return GetEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    public class CmdProject : CmdContainer
    {
        public TreeViewModel ParentTreeViewModel { get; set; }

        private bool isStartupProject = false;
        public bool IsStartupProject { get => isStartupProject; set => SetAndNotify(value, ref isStartupProject); }

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
        
        public Guid Kind { get; set; }
        
        public CmdProject(Guid id, Guid kind, string displayName, IEnumerable<CmdBase> items = null, bool isExpanded = true)
            : base(id, displayName, items, isExpanded)
        {
            Kind = kind;
        }

        public CmdProject(Guid kind, string displayName, IEnumerable<CmdBase> items = null, bool isExpanded = true)
            : this(Guid.NewGuid(), kind, displayName, items, isExpanded)
        { }

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

    public class CmdGroup : CmdContainer
    { 
        public override bool IsEditable => true;

        public CmdGroup(Guid id, string name, IEnumerable<CmdBase> items = null, bool isExpanded = true) 
            : base(id, name, items, isExpanded)
        { }

        public CmdGroup(string name, IEnumerable<CmdBase> items = null, bool isExpanded = true) 
            : this(Guid.NewGuid(), name, items, isExpanded)
        { }

        public override CmdBase Copy()
        {
            return new CmdGroup(Value, Items.Select(cmd => cmd.Copy())) {isExpanded = isExpanded};
        }
    }

    public class CmdArgument : CmdBase
    {
        public override bool IsEditable => true;

        public new bool IsChecked
        {
            get => base.IsChecked == true;
            set => base.IsChecked = value;
        }

        public CmdArgument(Guid id, string arg, bool isChecked = false)
            : base(id, arg, isChecked)
        { }
        
        public CmdArgument(string arg, bool isChecked = false) 
            : this(Guid.NewGuid(), arg, isChecked)
        { }

        public override CmdBase Copy()
        {
            return new CmdArgument(Value, IsChecked);
        }
    }
}
