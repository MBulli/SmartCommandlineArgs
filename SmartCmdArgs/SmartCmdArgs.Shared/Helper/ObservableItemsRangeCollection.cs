using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace SmartCmdArgs.Helper
{
    public class ObservableItemsRangeCollection<T> : ObservableRangeCollection<T>
    {
        public event EventHandler<CollectionItemPropertyChangedEventArgs<T>> ItemPropertyChanged;
        public event EventHandler<CollectionItemPropertyChangedDetailedEventArgs<T>> ItemPropertyChangedDetailed;

        public override void InsertRange(int index, IEnumerable<T> collection, NotifyCollectionChangedAction notificationMode = NotifyCollectionChangedAction.Add)
        {
            var list = collection.ToList();
            base.InsertRange(index, list, notificationMode);
            SubscribeToItems(list);
        }

        public override void RemoveRange(IEnumerable<T> collection, NotifyCollectionChangedAction notificationMode = NotifyCollectionChangedAction.Remove)
        {
            var list = collection.ToList();
            UnsubscribeToItems(list);
            base.RemoveRange(list, notificationMode);
        }

        public override void ReplaceRange(IEnumerable<T> collection, bool reset = false)
        {
            UnsubscribeToItems(this);
            var list = collection.ToList();
            base.ReplaceRange(list, reset);
            SubscribeToItems(list);
        }

        protected override void ClearItems()
        {
            UnsubscribeToItems(this);
            base.ClearItems();
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            SubscribeToItem(item);
        }

        protected override void RemoveItem(int index)
        {
            UnsubscribeToItem(this[index]);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            UnsubscribeToItem(this[index]);
            base.SetItem(index, item);
            SubscribeToItem(item);
        }

        private void SubscribeToItems(IEnumerable<T> items)
        {
            foreach (var item in items)
                SubscribeToItem(item);
        }

        private void UnsubscribeToItems(IEnumerable<T> items)
        {
            foreach (var item in items)
                UnsubscribeToItem(item);
        }

        private void SubscribeToItem(T item)
        {
            if (item is INotifyPropertyChanged propertyChanged)
                propertyChanged.PropertyChanged += OnItemPropertyChanged;
            if (item is INotifyPropertyChangedDetailed propertyChangedDetailed)
                propertyChangedDetailed.PropertyChangedDetailed += PropertyChangedDetailedOnPropertyChangedDetailed;
        }

        private void UnsubscribeToItem(T item)
        {
            if (item is INotifyPropertyChanged propertyChanged)
                propertyChanged.PropertyChanged -= OnItemPropertyChanged;
            if (item is INotifyPropertyChangedDetailed propertyChangedDetailed)
                propertyChangedDetailed.PropertyChangedDetailed -= PropertyChangedDetailedOnPropertyChangedDetailed;
        }

        private void PropertyChangedDetailedOnPropertyChangedDetailed(object sender, PropertyChangedDetailedEventArgs e)
        {
            ItemPropertyChangedDetailed?.Invoke(this, new CollectionItemPropertyChangedDetailedEventArgs<T>((T)sender, e));
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ItemPropertyChanged?.Invoke(this, new CollectionItemPropertyChangedEventArgs<T>((T)sender, e.PropertyName));
        }
    }
}
