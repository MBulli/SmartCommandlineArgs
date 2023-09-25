using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using SmartCmdArgs.Helper;

namespace SmartCmdArgs.ViewModel
{
    public enum ArgumentType
    {
        CmdArg,
        EnvVar,
        WorkDir,
    }

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
                BubbleEvent(new CheckStateWillChangeEvent(this));
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || InExclusiveModeContainer)
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

        public bool IsActive => true;

        private string _projectConfig = null;
        public string ProjectConfig { get => _projectConfig; protected set => OnProjectConfigChanged(this._projectConfig, value); }
        public string UsedProjectConfig => _projectConfig ?? Parent?.UsedProjectConfig;

        private string _projectPlatform = null;
        public string ProjectPlatform { get => _projectPlatform; protected set => OnProjectPlatformChanged(this._projectPlatform, value); }
        public string UsedProjectPlatform => _projectPlatform ?? Parent?.UsedProjectPlatform;

        private string _launchProfile = null;
        public string LaunchProfile { get => _launchProfile; protected set => OnLaunchProfileChanged(this._launchProfile, value); }
        public string UsedLaunchProfile => _launchProfile ?? Parent?.UsedLaunchProfile;

        public bool InExclusiveModeContainer => Parent?.ExclusiveMode ?? false;

        public virtual Guid ProjectGuid => Parent?.ProjectGuid ?? Guid.Empty;

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
            
            if (InExclusiveModeContainer && IsChecked != false)
            {
                var checkedItems = Parent.Items.Where(item => item.IsChecked != false);
                foreach (var item in checkedItems)
                {
                    if (item != this)
                    {
                        item.OnIsCheckedChanged(item.isChecked, false, true);
                    }
                }
            }

            // TODO: Only bubble event if we're the origin.
            if (oldValue != newValue)
            {
                BubbleEvent(new CheckStateChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnProjectConfigChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _projectConfig, nameof(ProjectConfig));

            if (oldValue != newValue)
            {
                BubbleEvent(new ProjectConfigChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnProjectPlatformChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _projectPlatform, nameof(ProjectPlatform));

            if (oldValue != newValue)
            {
                BubbleEvent(new ProjectPlatformChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnLaunchProfileChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref _launchProfile, nameof(LaunchProfile));

            if (oldValue != newValue)
            {
                BubbleEvent(new LaunchProfileChangedEvent(this, oldValue, newValue));
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

        public void SetIsCheckedAndNotifyingParent(bool? value)
        {
            OnIsCheckedChanged(IsChecked, value, true);
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

        protected bool exclusiveMode;
        public bool ExclusiveMode
        {
            get => exclusiveMode;
            set => OnExclusiveModeChanged(exclusiveMode, value);
        }

        protected string delimiter;
        public string Delimiter
        {
            get => delimiter;
            set => OnDelimiterChanged(delimiter, value);
        }

        protected string prefix;
        public string Prefix
        {
            get => prefix ?? "";
            set => OnPrefixChanged(prefix, value);
        }

        protected string postfix;
        public string Postfix
        {
            get => postfix ?? "";
            set => OnPostfixChanged(postfix, value);
        }

        public ObservableRangeCollection<CmdBase> Items { get; }
        public ICollectionView ItemsView { get; }
        protected virtual Predicate<CmdBase> FilterPredicate => Parent?.FilterPredicate;

        public IEnumerable<CmdContainer> AllContainer => this.OfType<CmdContainer>();

        public IEnumerable<CmdArgument> AllArguments => this.OfType<CmdArgument>();

        public IEnumerable<CmdArgument> CheckedArguments => AllArguments.Where(arg => arg.IsChecked);

        public IEnumerable<CmdBase> SelectedItems => this.Where(item => item.IsSelected);

        public CmdContainer(Guid id, string value, IEnumerable<CmdBase> subItems, bool isExpanded, bool exclusiveMode, string delimiter, string prefix, string postfix)
            : base(id, value)
        {
            this.isExpanded = isExpanded;
            this.exclusiveMode = exclusiveMode;
            this.delimiter = delimiter;
            this.prefix = prefix;
            this.postfix = postfix;

            Items = new ObservableRangeCollection<CmdBase>();

            Items.CollectionChanged += ItemsOnCollectionChanged;

            ItemsView = CollectionViewSource.GetDefaultView(Items);

            ItemsView.Filter = o =>
            {
                if (FilterPredicate != null && o is CmdBase item)
                    return FilterPredicate(item);
                return true;
            };

            if (subItems != null)
                AddRange(subItems);
        }

        public CmdContainer(string value, IEnumerable<CmdBase> items = null, bool isExpanded = true, bool exclusiveMode = false, string delimiter = " ", string prefix = "", string postfix = "") 
            : this(Guid.NewGuid(), value, items, isExpanded, exclusiveMode, delimiter, prefix, postfix)
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

        private void OnExclusiveModeChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref exclusiveMode, nameof(ExclusiveMode));

            var checkedFound = false;

            foreach (var item in Items)
            {
                item.OnNotifyPropertyChanged(nameof(InExclusiveModeContainer));

                if (exclusiveMode && (item.IsChecked ?? true))
                {
                    if (checkedFound)
                        item.SetIsCheckedAndNotifyingParent(false);
                    else
                        checkedFound = true;
                }
            }

            if (oldValue != newValue)
            {
                BubbleEvent(new ExclusiveModeChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnDelimiterChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref delimiter, nameof(Delimiter));

            if (oldValue != newValue)
            {
                BubbleEvent(new DelimiterChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnPrefixChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref prefix, nameof(Prefix));

            if (oldValue != newValue)
            {
                BubbleEvent(new PrefixChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnPostfixChanged(string oldValue, string newValue)
        {
            SetAndNotify(newValue, ref postfix, nameof(Postfix));

            if (oldValue != newValue)
            {
                BubbleEvent(new PostfixChangedEvent(this, oldValue, newValue));
            }
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
            bool? value = newValue;
            if (newValue == true && ExclusiveMode && Items.Count > 1)
            {
                value = null;
            }

            base.OnIsCheckedChanged(oldValue, value, notifyParent);

            if (ExclusiveMode)
            {
                foreach (var item in Items.Skip(1))
                {
                    item.SetIsCheckedWithoutNotifyingParent(false);
                }
                Items.FirstOrDefault()?.SetIsCheckedWithoutNotifyingParent(newValue);
            }
            else
            {
                foreach (var item in Items)
                {
                    item.SetIsCheckedWithoutNotifyingParent(newValue);
                }
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

        public void InsertRange(int idx, IEnumerable<CmdBase> items)
        {
            var list = items.ToList();

            if (ExclusiveMode)
            {
                var checkedFound = false;
                foreach (var item in list)
                {
                    if (item.IsChecked ?? true)
                    {
                        if (checkedFound)
                            item.SetIsCheckedWithoutNotifyingParent(false);
                        else
                            checkedFound = true;
                    }
                }

                if (checkedFound)
                {
                    foreach (var item in Items)
                    {
                        item.SetIsCheckedWithoutNotifyingParent(false);
                    }
                }
            }

            Items.InsertRange(idx, list);
        }

        public void AddRange(IEnumerable<CmdBase> items)
        {
            InsertRange(Items.Count, items);
        }

        public void Insert(int idx, CmdBase item)
        {
            if (ExclusiveMode && (item.IsChecked ?? true))
            {
                foreach (var loopItem in Items)
                {
                    loopItem.SetIsCheckedWithoutNotifyingParent(false);
                }
            }
            Items.Insert(idx, item);
        }

        public void Add(CmdBase item)
        {
            Insert(Items.Count, item);
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

        internal void MoveSelectedEntries(int moveDirection)
        {
            var selectedItems = Items.Where(item => item.IsSelected).ToList();
            var itemIndexList = selectedItems.Select(item => new KeyValuePair<CmdBase, int>(item, Items.IndexOf(item))).ToList();

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

            Items.OfType<CmdContainer>().ForEach(container => container.MoveSelectedEntries(moveDirection));
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

    public class CmdGroup : CmdContainer
    { 
        public override bool IsEditable => true;

        public new string ProjectConfig
        {
            get => base.ProjectConfig;
            set => base.ProjectConfig = value;
        }

        public new string ProjectPlatform
        {
            get => base.ProjectPlatform;
            set => base.ProjectPlatform = value;
        }

        public new string LaunchProfile
        {
            get => base.LaunchProfile;
            set => base.LaunchProfile = value;
        }

        public CmdGroup(Guid id, string name, IEnumerable<CmdBase> items, bool isExpanded, bool exclusiveMode, string projConf, string projPlatform, string launchProfile, string delimiter, string prefix, string postfix)
            : base(id, name, items, isExpanded, exclusiveMode, delimiter, prefix, postfix)
        {
            base.ProjectConfig = projConf;
            base.ProjectPlatform = projPlatform;
            base.LaunchProfile = launchProfile;
        }

        public CmdGroup(string name, IEnumerable<CmdBase> items = null, bool isExpanded = true, bool exclusiveMode = false, string projConf = null, string projPlatform = null, string launchProfile = null, string delimiter = " ", string prefix = "", string postfix = "")
            : this(Guid.NewGuid(), name, items, isExpanded, exclusiveMode, projConf, projPlatform, launchProfile, delimiter, prefix, postfix)
        { }

        public override CmdBase Copy()
        {
            return new CmdGroup(
                Value,
                Items.Select(cmd => cmd.Copy()),
                isExpanded,
                ExclusiveMode,
                ProjectConfig,
                ProjectPlatform,
                LaunchProfile,
                Delimiter,
                Postfix,
                Prefix);
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

        private bool isActive = true;
        public new bool IsActive { get => isActive; set => SetAndNotify(value, ref isActive); }

        private ArgumentType argumentType;
        public ArgumentType ArgumentType { get => argumentType; set => OnArgumentTypeChanged(argumentType, value); }

        private bool defaultChecked;
        public bool DefaultChecked { get => defaultChecked; set => OnDefaultCheckedChanged(defaultChecked, value); }

        public CmdArgument(Guid id, ArgumentType argType, string arg, bool isChecked = false, bool defaultChecked = false)
            : base(id, arg, isChecked)
        {
            this.argumentType = argType;
            this.defaultChecked = defaultChecked;
        }

        public CmdArgument(ArgumentType argType, string arg, bool isChecked = false, bool defaultChecked = false)
            : this(Guid.NewGuid(), argType, arg, isChecked, defaultChecked)
        { }

        private void OnDefaultCheckedChanged(bool oldValue, bool newValue)
        {
            SetAndNotify(newValue, ref defaultChecked, nameof(DefaultChecked));

            if (oldValue != newValue)
            {
                BubbleEvent(new DefaultCheckedChangedEvent(this, oldValue, newValue));
            }
        }

        private void OnArgumentTypeChanged(ArgumentType oldValue, ArgumentType newValue)
        {
            SetAndNotify(newValue, ref argumentType, nameof(ArgumentType));

            if (oldValue != newValue)
            {
                BubbleEvent(new ArgumentTypeChangedEvent(this, oldValue, newValue));
            }
        }

        public override CmdBase Copy()
        {
            return new CmdArgument(ArgumentType, Value, IsChecked, DefaultChecked);
        }
    }
}
