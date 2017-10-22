using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Helper;
using System.Windows.Data;
using System.IO;
using System.Windows;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.ViewModel
{
    public class ListViewModel : PropertyChangedBase
    {
        public ObservableCollectionEx<CmdArgItem> DataCollection { get; }

        private System.Collections.IList _selectedItems;
        public System.Collections.IList SelectedItems
        {
            get { return _selectedItems; } set { _selectedItems = value; OnSelectedItemsChanged(); }
        }

        public bool HasSelectedItems
        {
            get { return _selectedItems != null && _selectedItems.Count != 0; }
        }

        [Newtonsoft.Json.JsonIgnore]
        public ICollectionView DataCollectionView { get; }

        public event EventHandler<System.Collections.IList> SelectedItemsChanged;

        public ListViewModel()
        {
            DataCollection = new ObservableCollectionEx<CmdArgItem>();
            DataCollectionView = CollectionViewSource.GetDefaultView(DataCollection);
			
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                DataCollection.Add(new CmdArgItem() { Enabled = true, Command = @"C:\Users\Markus\Desktop\" });
                DataCollection.Add(new CmdArgItem() { Enabled = false, Command = "Hello World" });
                DataCollection.Add(new CmdArgItem() { Enabled = true, Command = "A very long commandline to test very long commandlines to see how very long commandlines work in our UI." });
            }

            
        }

        // CRUD Operations
        public CmdArgItem AddNewItem(string command, bool enabled = true)
        {
            CmdArgItem item = new CmdArgItem() {
                Id = Guid.NewGuid(),
                Command = command,
                Enabled = enabled};

            DataCollection.Add(item);
            return item;
        }

        internal void MoveEntriesDown(IEnumerable<CmdArgItem> items)
        {
            var itemIndexList =  items.Select(item => new KeyValuePair<CmdArgItem, int>(item, DataCollection.IndexOf(item))).ToList();

            if (itemIndexList.Max(pair => pair.Value) >= DataCollection.Count - 1)
                return;

            itemIndexList.Sort((pairA, pairB) => pairB.Value.CompareTo(pairA.Value));

            foreach (var itemIndexPair in itemIndexList)
            {
                DataCollection.Move(itemIndexPair.Key, itemIndexPair.Value + 1);
            }
        }

        internal void MoveEntriesUp(IEnumerable<CmdArgItem> items)
        {
            var itemIndexList = items.Select(item => new KeyValuePair<CmdArgItem, int>(item, DataCollection.IndexOf(item))).ToList();

            if (itemIndexList.Min(pair => pair.Value) <= 0)
                return;

            itemIndexList.Sort((pairA, pairB) => pairA.Value.CompareTo(pairB.Value));

            foreach (var itemIndexPair in itemIndexList)
            {
                DataCollection.Move(itemIndexPair.Key, itemIndexPair.Value - 1);
            }
        }

        internal void ToogleEnabledForItem(CmdArgItem item, bool exclusiveMode)
        {
            bool newState = !item.Enabled;

            // If exclusiveMode is true only one item at a time should be enabled
            if (exclusiveMode)
            {
                using (DataCollection.OpenBulkChange())
                {
                    int enabledCount = 0;
                    foreach (var cmdArgItem in DataCollection)
                    {
                        if (cmdArgItem.Enabled)
                            enabledCount++;

                        cmdArgItem.Enabled = false;
                    }

                    // If more than one items are enabled disable all, but not 'item'
                    if (enabledCount > 1)
                        newState = true;

                    item.Enabled = newState;
                }
            }
            // If item is selected set enable state of all selected items
            else if (SelectedItems.Contains(item))
            {
                using (DataCollection.OpenBulkChange())
                {
                    foreach (CmdArgItem i in SelectedItems)
                    {
                        i.Enabled = newState;
                    }
                }
            }
            // Just toggle the item
            else
            {
                item.Enabled = newState;
            }
        }

        public void SetStringFilter(string filter, bool matchCase = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(filter))
                {
                    DataCollectionView.Filter = _ => true;
                }
                else
                {
                    if (matchCase)
                    {
                        DataCollectionView.Filter = item => ((CmdArgItem) item).Command.Contains(filter);
                    }
                    else
                    {
                        filter = filter.ToLower();
                        DataCollectionView.Filter = item => ((CmdArgItem) item).Command.ToLower().Contains(filter);
                    }
                }
            });
        }

        protected virtual void OnSelectedItemsChanged()
        {
            SelectedItemsChanged?.Invoke(this, SelectedItems);
        }
    }
}
