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
        private readonly BindingListEx<CmdArgItem> dataCollection;
        private readonly ICollectionView dataView;

        public ICollectionView CmdLineItems { get { return dataView; } }

        public event ListChangedEventHandler ArgumentListChanged;

        public ListViewModel()
        {
            dataCollection = new BindingListEx<CmdArgItem>();
            dataView = new ListCollectionView(dataCollection);
            
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                dataCollection.Add(new CmdArgItem() { Enabled = true, Command = @"C:\Users\Markus\Desktop\" });
                dataCollection.Add(new CmdArgItem() { Enabled = false, Command = "Hello World" });
                dataCollection.Add(new CmdArgItem() { Enabled = true, Command = "A very long commandline to test very long commandlines to see how very long commandlines work in our UI." });
            }

            // Redirect list change events
            dataCollection.ListChanged += OnArgumentListChanged;
        }

        public void PopulateFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            StreamReader sr = new StreamReader(stream);
            string jsonStr = sr.ReadToEnd();

            var entries = JsonConvert.DeserializeObject<List<CmdArgItem>>(jsonStr);

            if (entries != null)
            {
                this.dataCollection.AddRange(entries);
            }
        }

        public void StoreToStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            string jsonStr = JsonConvert.SerializeObject(this.dataCollection);

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

            dataCollection.Add(item);
            return item;
        }

        internal void RemoveEntries(IEnumerable<CmdArgItem> items)
        {
            dataCollection.RemoveRange(items);
        }

        internal void MoveEntriesDown(IEnumerable<CmdArgItem> items)
        {
            var itemIndexDictionary =  items.ToDictionary(item => item, item => dataCollection.IndexOf(item));

            if (itemIndexDictionary.Values.Max() >= dataCollection.Count - 1)
                return;

            foreach (var itemIndexPair in itemIndexDictionary)
            {
                dataCollection.Move(itemIndexPair.Key, itemIndexPair.Value - 1);
            }
        }

        internal void MoveEntriesUp(IEnumerable<CmdArgItem> items)
        {
            var itemIndexDictionary = items.ToDictionary(item => item, item => dataCollection.IndexOf(item));

            if (itemIndexDictionary.Values.Min() <= 0)
                return;

            foreach (var itemIndexPair in itemIndexDictionary)
            {
                dataCollection.Move(itemIndexPair.Key, itemIndexPair.Value + 1);
            }
        }

        public void FilterByProject(string project)
        {
            CmdLineItems.Filter = e => ((CmdArgItem)e).Project == project;
        }

        private void OnArgumentListChanged(object sender, ListChangedEventArgs args)
        {
            ArgumentListChanged?.Invoke(this, args);
        }
    }
}
