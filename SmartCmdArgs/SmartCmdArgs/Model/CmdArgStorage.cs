using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartCmdArgs.Helper;
using SmartCmdArgs.ViewModel;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace SmartCmdArgs.Model
{
    class CmdArgStorage
    {
        private static readonly Lazy<CmdArgStorage> singletonLazy = new Lazy<CmdArgStorage>(() => new CmdArgStorage());

        // TODO: For now use singleton, better way would be services
        public static CmdArgStorage Instance { get { return singletonLazy.Value; } }

        private List<CmdArgStorageEntry> entryList; 

        public IReadOnlyList<CmdArgStorageEntry> Entries { get { return entryList; } }
        public string StartupProject { get; private set; }

        public IReadOnlyList<CmdArgStorageEntry> StartupProjectEntries
        {
            get { return EntriesFilteredByProject(StartupProject); }
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
            }
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

        // CRUD operations
        public CmdArgStorageEntry AddEntry(string command, bool enabled = true)
        {
            return AddEntry(command, StartupProject, enabled);
        }

        public CmdArgStorageEntry AddEntry(string command, string uniquePrjName, bool enabled = true)
        {
            if (string.IsNullOrEmpty(uniquePrjName))
                throw new ArgumentNullException("uniquePrjName");

            var newEntry = new CmdArgStorageEntry
            {
                Id = Guid.NewGuid(),
                Enabled = enabled,
                Project = uniquePrjName,
                Command = command
            };

            entryList.Add(newEntry);
            return newEntry;
        }

        public void RemoveEntryById(Guid id)
        {
            var item = FindEntryById(id);
            if (item != null)
            {
                entryList.Remove(item);
                OnItemChanged(item);
            }
        }

        public void UpdateCommandById(Guid id, string newCommand)
        {
            var item = FindEntryById(id);
            if (item != null)
            {
                item.Command = newCommand;
                OnItemChanged(item);
            }
        }

        public void UpdateEnabledById(Guid id, bool newEnabled)
        {
            var item = FindEntryById(id);
            if (item != null)
            {
                item.Enabled = newEnabled;
                OnItemChanged(item);
            }
        }

        public void UpdateStartupProject(string newProjName)
        {
            if (StartupProject != newProjName)
            {
                StartupProject = newProjName;
                OnStartupProjectChanged();
            }
        }

        public IReadOnlyList<CmdArgStorageEntry> EntriesFilteredByProject(string project)
        {
            return entryList.FindAll(entry => entry.Project == project);
        }

        private CmdArgStorageEntry FindEntryById(Guid id)
        {
            return entryList.Find(entry => entry.Id == id);
        }

        public event EventHandler<CmdArgStorageEntry> ItemChanged;
        protected virtual void OnItemChanged(CmdArgStorageEntry item)
        {
            ItemChanged?.Invoke(this, item);
        }

        public event EventHandler StartupProjectChanged;
        protected virtual void OnStartupProjectChanged()
        {
            StartupProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    class CmdArgStorageEntry
    {
        private Guid id;
        private bool enabled;
        private string project;
        private string command;

        public Guid Id
        {
            get { return id; }
            set { id = value; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public string Project
        {
            get { return project; }
            set { project = value; }
        }

        public string Command
        {
            get { return command; }
            set { command = value; }
        }
    }
}
