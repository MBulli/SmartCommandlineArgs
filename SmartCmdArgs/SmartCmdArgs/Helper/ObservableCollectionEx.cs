using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    public class ObservableCollectionEx<T> : ObservableCollection<T>
         where T : INotifyPropertyChanged
    {
        private bool raiseEvents = true;

        public event EventHandler<CollectionItemPropertyChangedEventArgs<T>> ItemPropertyChanged;

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);

            item.PropertyChanged += OnItemPropertyChanged;
        }

        protected override void RemoveItem(int index)
        {
            T oldItem = this[index];
            base.RemoveItem(index);

            oldItem.PropertyChanged -= OnItemPropertyChanged;
        }

        protected override void SetItem(int index, T item)
        {
            T oldItem = this[index];

            base.SetItem(index, item);

            oldItem.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }

        protected override void ClearItems()
        {
            List<T> oldItems = new List<T>(Items);

            base.ClearItems();

            foreach (T item in oldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == this)
                throw new InvalidOperationException("Can't add range equals self.");

            try
            {
                raiseEvents = false;

                foreach (var item in items)
                {
                    Add(item);
                }

                raiseEvents = true;
                //OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items.ToList()));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
            finally
            {
                raiseEvents = true;
            }
        }
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == this)
                throw new InvalidOperationException("Can't remove range equals self.");

            try
            {
                raiseEvents = false;

                List<T> removedItems = new List<T>();
                foreach (var item in items)
                {
                    if (Remove(item))
                        removedItems.Add(item);
                }

                raiseEvents = true;
                if (removedItems.Count > 0)
                {
                    //OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
            finally
            {
                raiseEvents = true;
            }
        }

        public void Move(T item, int index)
        {
            int idx = this.IndexOf(item);
            if (idx != -1)
            {
                MoveItem(idx, index);
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (raiseEvents)
            {
                base.OnCollectionChanged(e);
            }
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (raiseEvents)
            {
                ItemPropertyChanged?.Invoke(this, new CollectionItemPropertyChangedEventArgs<T>((T)sender, e.PropertyName));
            }
        }
    }

    public class CollectionItemPropertyChangedEventArgs<T> : PropertyChangedEventArgs
    {
        public T Item { get; }

        public CollectionItemPropertyChangedEventArgs(T item, string propertyName)
            : base(propertyName)
        {
            Item = item;
        }
    }
}
