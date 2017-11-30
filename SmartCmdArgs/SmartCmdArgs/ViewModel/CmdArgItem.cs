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
using SmartCmdArgs.Helper;

namespace SmartCmdArgs.ViewModel
{
    public class CmdBase : PropertyChangedBase
    {
        public Guid Id { get; }

        private CmdContainer parent;
        public CmdContainer Parent { get => parent; set => SetAndNotify(value, ref this.parent); }

        private string value;
        public string Value { get => value; set => SetAndNotify(value, ref this.value); }

        protected bool? isChecked;
        public bool? IsChecked { get => isChecked; set => OnIsCheckedChanged(isChecked, value, true); }

        protected bool isSelected;
        public bool IsSelected {
            get => isSelected;
            set
            {
                SetAndNotify(value, ref isSelected);
                Parent?.OnChildSelectionChanged(this);
            }
        }

        public virtual bool IsEditable => false;

        public CmdBase(Guid id, string value, bool? isChecked = false)
        {
            if (id == Guid.Empty)
                id = Guid.NewGuid();
            Id = id;
            this.value = value;
            this.isChecked = isChecked;
        }

        public void ToggleCheckedState()
        {
            if (IsChecked == null)
                IsChecked = false;
            else
                IsChecked = !IsChecked;
        }

        protected virtual void OnIsCheckedChanged(bool? oldValue, bool? newValue, bool notifyParent)
        {
            SetAndNotify(newValue, ref this.isChecked, nameof(IsChecked));

            if (notifyParent)
            {
                parent?.OnChildIsCheckedChanged(oldValue, newValue);
            }
        }

        protected virtual void OnChildIsCheckedChanged(bool? oldValue, bool? newValue)
        {}

        public void SetIsCheckedWithoutNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, false);
        }

        private string editBackupValue;

        private bool isInEditMode;
        public bool IsInEditMode { get => isInEditMode; private set => SetAndNotify(value, ref this.isInEditMode); }

        public event EventHandler<EditMode> EditModeChanged;
        public enum EditMode
        {
            BeganEdit, BeganEditAndReset, CanceledEdit, CommitedEdit
        }

        public void BeginEdit(string initialValue = null)
        {
            ThrowIfNotEditable();

            if (!IsInEditMode)
            {
                editBackupValue = Value;
                if (initialValue != null) Value = initialValue;
                IsInEditMode = true;
                EditModeChanged?.Invoke(this, initialValue != null ? EditMode.BeganEditAndReset : EditMode.BeganEdit);
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
                EditModeChanged?.Invoke(this, EditMode.CanceledEdit);
            }
        }

        public void CommitEdit()
        {
            ThrowIfNotEditable();

            if (IsInEditMode)
            {
                editBackupValue = null;
                IsInEditMode = false;
                EditModeChanged?.Invoke(this, EditMode.CommitedEdit);
            }
        }

        private void ThrowIfNotEditable()
        {
            if (!IsEditable)
                throw new InvalidOperationException("Can't execute edit operation on a not editable item!");
        }

        public virtual CmdBase Copy()
        {
            return null;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}{{{Value}:{(IsChecked == null ? "▄" : (IsChecked.Value ? "✓" : "❌"))}:{(IsSelected ? "█" : "-")}}}";
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

        public ObservableCollectionEx<CmdBase> Items { get; }
        public ICollectionView ItemsView { get; }
        protected virtual Predicate<CmdBase> FilterPredicate => Parent?.FilterPredicate;

        protected void RefreshFilters()
        {
            Items.OfType<CmdContainer>().ForEach(container => container.RefreshFilters());
            ItemsView.Refresh();
        }

        public IEnumerable<CmdContainer> AllContainer => this.OfType<CmdContainer>();

        public IEnumerable<CmdArgument> AllArguments => this.OfType<CmdArgument>();

        public IEnumerable<CmdBase> SelectedItems => this.Where(item => item.IsSelected);

        public IEnumerable<CmdArgument> CheckedArguments => this.OfType<CmdArgument>().Where(arg => arg.IsChecked);
        
        public IEnumerable<CmdContainer> ExpandedContainer => this.OfType<CmdContainer>().Where(arg => arg.IsExpanded);

        public CmdContainer(Guid id, string value, IEnumerable<CmdBase> items = null, bool isExpanded = true)
            : base(id, value)
        {
            this.isExpanded = isExpanded;

            Items = new ObservableCollectionEx<CmdBase>();

            foreach (var item in items ?? Enumerable.Empty<CmdBase>())
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

        public CmdArgument AddNewArgument(string command, bool enabled = true)
        {
            var item = new CmdArgument(command, enabled);
            Items.Add(item);
            return item;
        }

        public CmdGroup AddNewGroup(string command)
        {
            var group = new CmdGroup(command);
            Items.Add(group);
            return group;
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

        public virtual void OnChildSelectionChanged(CmdBase e)
        {
            Parent?.OnChildSelectionChanged(e);
        }
        
        public IEnumerator<CmdBase> GetEnumerator()
        {
            foreach (var item in Items)
            {
                yield return item;
                if (item is CmdContainer con)
                {
                    foreach (var conItem in con)
                        yield return conItem;
                }
            }
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

        public bool isFocusedProject = false;
        public bool IsFocusedProject { get => isFocusedProject; set => SetAndNotify(value, ref isFocusedProject); }

        protected override Predicate<CmdBase> FilterPredicate => Filter;
        private Predicate<CmdBase> filter;
        public Predicate<CmdBase> Filter
        {
            get => filter; set { filter = value; RefreshFilters(); }
        }

        public string UniqueName { get; set; }

        public CmdProject(Guid id, string uniqueName, string displayName, IEnumerable<CmdBase> items = null, bool isExpanded = true)
            : base(id, displayName, items, isExpanded)
        {
            UniqueName = uniqueName;
        }

        public CmdProject(string uniqueName, string displayName, IEnumerable<CmdBase> items = null, bool isExpanded = true)
            : this(Guid.NewGuid(), uniqueName, displayName, items, isExpanded)
        { }

        public override CmdBase Copy()
        {
            return new CmdProject(UniqueName, Value, Items.Select(cmd => cmd.Copy())) {isExpanded = isExpanded, Filter = Filter};
        }

        public override void OnChildSelectionChanged(CmdBase e)
        {
            ParentTreeViewModel?.OnItemSelectionChanged(e);
        }
    }

    public class CmdGroup : CmdContainer
    { 
        public override bool IsEditable => true;

        public CmdGroup(Guid id, string value, IEnumerable<CmdBase> items = null, bool isExpanded = true) 
            : base(id, value, items, isExpanded)
        { }

        public CmdGroup(string value, IEnumerable<CmdBase> items = null, bool isExpanded = true) 
            : this(Guid.NewGuid(), value, items, isExpanded)
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

        public CmdArgument(Guid id, string value, bool isChecked = false)
            : base(id, value, isChecked)
        { }
        
        public CmdArgument(string value, bool isChecked = false) 
            : this(Guid.NewGuid(), value, isChecked)
        { }

        public override CmdBase Copy()
        {
            return new CmdArgument(Value, IsChecked);
        }
    }
}
