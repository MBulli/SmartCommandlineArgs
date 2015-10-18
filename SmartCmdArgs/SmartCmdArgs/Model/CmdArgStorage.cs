using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            var entries = JsonConvert.DeserializeObject<CmdArgStorageEntry[]>(jsonStr);
        }

        public void StoreToStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            string jsonStr = JsonConvert.SerializeObject(this.entryList);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
        }
    }

    class CmdArgStorageEntry
    {
        public bool Enabled { get; set; }
        public string Project { get; set; }
        public string Command { get; set; }
    }
}
