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
            dataView = CollectionViewSource.GetDefaultView(dataCollection);

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

        internal void RemoveById(Guid id)
        {
            var itemToRemove = dataCollection.FirstOrDefault(item => item.Id == id);
            if (itemToRemove == null)
                return;

            dataCollection.Remove(itemToRemove);
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

        private void OnArgumentListChanged(object sender, ListChangedEventArgs args)
        {
            ArgumentListChanged?.Invoke(this, args);
        }
    }
}
