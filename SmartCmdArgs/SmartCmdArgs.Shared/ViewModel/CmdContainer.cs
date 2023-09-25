using SmartCmdArgs.Helper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace SmartCmdArgs.ViewModel
{
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
}
