using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Model;

namespace SmartCmdArgs.ViewModel
{
    public class CmdArgListViewModel : PropertyChangedBase
    {
        public ObservableCollection<CmdArgItem> CmdLineItems { get; private set; }

        public CmdArgListViewModel()
        {
            CmdLineItems = new ObservableCollection<CmdArgItem>();

            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                CmdLineItems.Add(new CmdArgItem() { Enabled = true, Value = @"C:\Users\Markus\Desktop\" });
                CmdLineItems.Add(new CmdArgItem() { Enabled = false, Value = "Hello World" });
            }

            AddAllCmdArgStoreEntries(CmdArgStorage.Instance.CurStartupProjectEntries);

            CmdArgStorage.Instance.EntryAdded += (sender, entry) => AddCmdArgStoreEntry(entry);
            CmdArgStorage.Instance.EntryRemoved += (sender, entry) => RemoveById(entry.Id);
            CmdArgStorage.Instance.EntriesReloaded += (sender, list) =>
            {
                foreach (var cmdLineItem in CmdLineItems)
                {
                    cmdLineItem.PropertyChanged -= OnPropertyChangedInItem;
                }
                CmdLineItems.Clear();
                AddAllCmdArgStoreEntries(list);
            };
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
            var itemToRemove = CmdLineItems.FirstOrDefault(item => item.Id == id);
            if (itemToRemove == null) return;
            CmdLineItems.Remove(itemToRemove);
            itemToRemove.PropertyChanged -= OnPropertyChangedInItem;
        }

        private void AddCmdArgStoreEntry(CmdArgStorageEntry entry)
        {
            CmdArgItem newItem = new CmdArgItem
            {
                Id = entry.Id,
                Enabled = entry.Enabled,
                Value = entry.Command
            };
            newItem.PropertyChanged += OnPropertyChangedInItem;
            CmdLineItems.Add(newItem);
        }

        private void OnPropertyChangedInItem(object sender, PropertyChangedEventArgs args)
        {
            var item = sender as CmdArgItem;
            if (args.PropertyName == "Value")
                CmdArgStorage.Instance.UpdateCommandById(item.Id, item.Value);
            else if (args.PropertyName == "Enabled")
                CmdArgStorage.Instance.UpdateEnabledById(item.Id, item.Enabled);
        }
    }
}
