using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            AddAllCmdArgStoreEntries(CmdArgStorage.Instance.CurStartupProjectEntries);

            CmdArgStorage.Instance.EntryAdded += (sender, entry) => AddCmdArgStoreEntry(entry);
            CmdArgStorage.Instance.EntryRemoved += (sender, entry) => RemoveById(entry.Id);
            CmdArgStorage.Instance.EntriesReloaded += (sender, list) =>
            {
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
            CmdLineItems.Remove(itemToRemove);
        }

        private void AddCmdArgStoreEntry(CmdArgStorageEntry entry)
        {
            CmdLineItems.Add(new CmdArgItem
            {
                Id = entry.Id,
                Enabled = entry.Enabled,
                Value = entry.Command
            });
        }
    }
}
