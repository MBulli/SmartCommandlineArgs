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

            AddAllCmdArgStoreEntries(CmdArgStorage.Instance.CurStartupProjectEntries);

            dataCollection.ListChanged += DataCollection_ListChanged;

            CmdArgStorage.Instance.EntryAdded += (sender, entry) => AddCmdArgStoreEntry(entry);
            CmdArgStorage.Instance.EntryRemoved += (sender, entry) => RemoveById(entry.Id);
            CmdArgStorage.Instance.EntriesReloaded += (sender, list) =>
            {
                dataCollection.Clear();
                AddAllCmdArgStoreEntries(list);
            };
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

        private void RemoveById(Guid id)
        {
            var itemToRemove = dataCollection.FirstOrDefault(item => item.Id == id);
            if (itemToRemove == null)
                return;

            dataCollection.Remove(itemToRemove);
        }

        private void AddCmdArgStoreEntry(CmdArgStorageEntry entry)
        {
            dataCollection.Add(new CmdArgItem
            {
                Id = entry.Id,
                Enabled = entry.Enabled,
                Value = entry.Command
            });
        }
    }
}
