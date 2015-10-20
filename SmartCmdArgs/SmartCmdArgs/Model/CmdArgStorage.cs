using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.ViewModel;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.Model
{
    class CmdArgStorage : PropertyChangedBase
    {
        private static readonly Lazy<CmdArgStorage> singletonLazy = new Lazy<CmdArgStorage>(() => new CmdArgStorage());

        // TODO: For now use singleton, better way would be services
        public static CmdArgStorage Instance { get { return singletonLazy.Value; } }

        private List<CmdArgStorageEntry> entryList; 

        public IReadOnlyList<CmdArgStorageEntry> Entries { get { return entryList; } }
        public string CurStartupProject { get; private set; }

        public IReadOnlyList<CmdArgStorageEntry> CurStartupProjectEntries
        {
            get { return entryList.FindAll(entry => entry.Project == CurStartupProject); }
        }

        private CmdArgStorage()
        {
            entryList = new List<CmdArgStorageEntry>();
        }

        public void PopulateFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<List<CmdArgStorageEntry>>(jsonStr);

            if (entries != null)
            {
                entryList = entries;
                entryList.ForEach(entry => entry.PropertyChanged += (sender, args) => OnEntryUpdated((CmdArgStorageEntry) sender));
            }

            OnEntriesReloaded();
        }

        public void StoreToStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            string jsonStr = JsonConvert.SerializeObject(this.entryList);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();
        }

        public void RemoveEntryById(Guid id)
        {
            CmdArgStorageEntry entryToRemove = FindEntryById(id);
            entryList.Remove(entryToRemove);
            OnEntryRemoved(entryToRemove);
        }

        public void AddEntry(string command, bool enabled = true)
        {
            var newEntry = new CmdArgStorageEntry
            {
                Id = Guid.NewGuid(),
                Enabled = enabled,
                Project = CurStartupProject,
                Command = command
            };
            newEntry.PropertyChanged += (sender, args) => OnEntryUpdated((CmdArgStorageEntry) sender);
            entryList.Add(newEntry);
            OnEntryAdded(newEntry);
        }

        public void UpdateCommandById(Guid id, string newCommand)
        {
            FindEntryById(id).Command = newCommand;
        }

        public void UpdateEnabledById(Guid id, bool newEnabled)
        {
            FindEntryById(id).Enabled = newEnabled;
        }

        public void UpdateStartupProject(string projName)
        {
            CurStartupProject = projName;
            OnEntriesReloaded();
        }

        private CmdArgStorageEntry FindEntryById(Guid id)
        {
            return entryList.Find(entry => entry.Id == id);
        }

        public event EventHandler<IReadOnlyList<CmdArgStorageEntry>> EntriesReloaded;
        protected virtual void OnEntriesReloaded()
        {
            EntriesReloaded?.Invoke(this, CurStartupProjectEntries);
        }

        public event EventHandler<CmdArgStorageEntry> EntryAdded;
        protected virtual void OnEntryAdded(CmdArgStorageEntry e)
        {
            EntryAdded?.Invoke(this, e);
        }

        public event EventHandler<CmdArgStorageEntry> EntryRemoved;
        protected virtual void OnEntryRemoved(CmdArgStorageEntry e)
        {
            EntryRemoved?.Invoke(this, e);
        }

        public event EventHandler<CmdArgStorageEntry> EntryUpdated;
        protected virtual void OnEntryUpdated(CmdArgStorageEntry e)
        {
            EntryUpdated?.Invoke(this, e);
        }
    }

    class CmdArgStorageEntry : PropertyChangedBase
    {
        private Guid id;
        private bool enabled;
        private string project;
        private string command;

        public Guid Id
        {
            get { return id; }
            set { id = value; OnNotifyPropertyChanged(); }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; OnNotifyPropertyChanged(); }
        }

        public string Project
        {
            get { return project; }
            set { project = value; OnNotifyPropertyChanged(); }
        }

        public string Command
        {
            get { return command; }
            set { command = value; OnNotifyPropertyChanged(); }
        }
    }
}
