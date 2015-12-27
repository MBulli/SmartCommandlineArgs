using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Helper;
using SmartCmdArgs.Model;
using System.Windows.Data;
using System.Windows.Input;
using System.IO;

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
        public CmdArgItem AddNewItem(string command, string project, bool enabled = true)
        {
            CmdArgItem item = new CmdArgItem() {
                Id = Guid.NewGuid(),
                Command = command,
                Enabled = enabled,
                Project = project };

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

        internal void ToogleEnabledForItems(IEnumerable<CmdArgItem> items)
        {
            CmdArgItem first = items.FirstOrDefault();

            if (first == null)
                return;

            bool newState = !first.Enabled;

            using (var _ = DataCollection.OpenBulkChange())
            {
                foreach (var item in SelectedItems.Cast<CmdArgItem>())
                {
                    item.Enabled = newState;
                }
            }
        }
        
        protected virtual void OnSelectedItemsChanged()
        {
            SelectedItemsChanged?.Invoke(this, SelectedItems);
        }
    }
}
