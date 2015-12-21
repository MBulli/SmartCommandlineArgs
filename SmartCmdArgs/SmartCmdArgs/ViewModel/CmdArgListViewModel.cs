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

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgListViewModel : PropertyChangedBase
    {
        private readonly BindingListEx<CmdArgItem> dataCollection;
        private readonly ICollectionView dataView;

        public ICollectionView CmdLineItems { get { return dataView; } } 


        public CmdArgListViewModel()
        {
            dataCollection = new BindingListEx<CmdArgItem>();
            dataView = CollectionViewSource.GetDefaultView(dataCollection);

            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                dataCollection.Add(new CmdArgItem() { Enabled = true, Value = @"C:\Users\Markus\Desktop\" });
                dataCollection.Add(new CmdArgItem() { Enabled = false, Value = "Hello World" });
                dataCollection.Add(new CmdArgItem() { Enabled = true, Value = "A very long commandline to test very long commandlines to see how very long commandlines work in our UI." });
            }

            dataCollection.ListChanged += DataCollection_ListChanged;
        }

        internal void SetListItems(IReadOnlyCollection<CmdArgStorageEntry> list)
        {
            dataCollection.Clear();
            AddAllCmdArgStoreEntries(list);
        }

        private void DataCollection_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemChanged)
            {
                CmdArgItem item = dataCollection[e.NewIndex];

                switch (e.PropertyDescriptor.Name)
                {
                    case nameof(CmdArgItem.Value):
                        CmdArgStorage.Instance.UpdateCommandById(item.Id, item.Value);
                        break;
                    case nameof(CmdArgItem.Enabled):
                        CmdArgStorage.Instance.UpdateEnabledById(item.Id, item.Enabled);
                        break;
                    default:
                        break;
                }
            }
            else if(e.ListChangedType == ListChangedType.ItemDeleted)
            {
                var item = e.GetDeletedItem<CmdArgItem>();
                CmdArgStorage.Instance.RemoveEntryById(item.Id);
            }
        }

        private void AddAllCmdArgStoreEntries(IReadOnlyCollection<CmdArgStorageEntry> entryList)
        {
            foreach (var cmdArgStorageEntry in entryList)
            {
                AddCmdArgStoreEntry(cmdArgStorageEntry);
            }
        }

        internal void RemoveById(Guid id)
        {
            var itemToRemove = dataCollection.FirstOrDefault(item => item.Id == id);
            if (itemToRemove == null)
                return;

            dataCollection.Remove(itemToRemove);
        }

        internal void AddCmdArgStoreEntry(CmdArgStorageEntry entry)
        {
            dataCollection.Add(new CmdArgItem
            {
                Id = entry.Id,
                Enabled = entry.Enabled,
                Value = entry.Command
            });
        }

        internal void MoveEntryDown(Guid id)
        {
            int index = GetIndexById(id);
            if (index > -1 && index < dataCollection.Count - 1)
                MoveEntry(index, index + 1);
        }

        internal void MoveEntryUp(Guid id)
        {
            int index = GetIndexById(id);
            if (index > 0)
                MoveEntry(index, index - 1);
        }

        internal void MoveEntry(int from, int to)
        {
            var item = dataCollection[from];
            dataCollection.RemoveAt(from);
            dataCollection.Insert(to, item);
        }

        private int GetIndexById(Guid id)
        {
            for (int i = 0; i < dataCollection.Count; i++)
            {
                if (dataCollection[i].Id == id)
                    return i;
            }
            return -1;
        }
    }
}
