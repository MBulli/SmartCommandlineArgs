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
        public BindingListEx<CmdArgItem> DataCollection { get; }

        public ICollectionView CmdLineItems { get; }

        public ListViewModel()
        {
            DataCollection = new BindingListEx<CmdArgItem>();
            CmdLineItems = new ListCollectionView(DataCollection);
            
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                DataCollection.Add(new CmdArgItem() { Enabled = true, Command = @"C:\Users\Markus\Desktop\" });
                DataCollection.Add(new CmdArgItem() { Enabled = false, Command = "Hello World" });
                DataCollection.Add(new CmdArgItem() { Enabled = true, Command = "A very long commandline to test very long commandlines to see how very long commandlines work in our UI." });
            }
        }

        public void PopulateFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<List<CmdArgItem>>(jsonStr);

            if (entries != null)
            {
                this.DataCollection.AddRange(entries);
            }
        }

        public void StoreToStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string jsonStr = JsonConvert.SerializeObject(this.DataCollection);

            StreamWriter sw = new StreamWriter(stream);
            sw.Write(jsonStr);
            sw.Flush();
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
            var itemIndexDictionary =  items.ToDictionary(item => item, item => DataCollection.IndexOf(item));

            if (itemIndexDictionary.Values.Max() >= DataCollection.Count - 1)
                return;

            foreach (var itemIndexPair in itemIndexDictionary)
            {
                DataCollection.Move(itemIndexPair.Key, itemIndexPair.Value - 1);
            }
        }

        internal void MoveEntriesUp(IEnumerable<CmdArgItem> items)
        {
            var itemIndexDictionary = items.ToDictionary(item => item, item => DataCollection.IndexOf(item));

            if (itemIndexDictionary.Values.Min() <= 0)
                return;

            foreach (var itemIndexPair in itemIndexDictionary)
            {
                DataCollection.Move(itemIndexPair.Key, itemIndexPair.Value + 1);
            }
        }

        public void FilterByProject(string project)
        {
            CmdLineItems.Filter = e => ((CmdArgItem)e).Project == project;
        }
    }
}
