using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Helper
{
    // Theres no way to find the deleted item in the item deleted handler.
    // Thats why we need this class
    class BindingListEx<T> : BindingList<T>
    {
        protected override void RemoveItem(int index)
        {
            // Store current values
            T itemToDelete = Items[index];
            bool raiseEvents = RaiseListChangedEvents;

            try
            {
                // Don't fire event twice
                RaiseListChangedEvents = false;
                base.RemoveItem(index);

                if (raiseEvents)
                    OnListChanged(new ListChangedItemDeletedEventArgs(itemToDelete, index));
            }
            finally
            {
                RaiseListChangedEvents = raiseEvents; // restore old value
            }
            
        }

        public void Move(T item, int index)
        {
            bool raiseListChangedEvents = this.RaiseListChangedEvents;
            try
            {
                this.RaiseListChangedEvents = false;
                int oldIndex = this.IndexOf(item);
                this.Remove(item);
                this.InsertItem(index, item);
                this.OnListChanged(new ListChangedEventArgs(ListChangedType.ItemMoved, index, oldIndex));
            }
            finally
            {
                this.RaiseListChangedEvents = raiseListChangedEvents;
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            bool raiseListChangedEvents = this.RaiseListChangedEvents;
            try
            {
                this.RaiseListChangedEvents = false;
                foreach (var item in items)
                {
                    this.Add(item);
                }
                this.OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1, -1));
            }
            finally
            {
                this.RaiseListChangedEvents = raiseListChangedEvents;
            }
        }

        public void RemoveRange(IEnumerable<T> items)
        {
            bool raiseListChangedEvents = this.RaiseListChangedEvents;
            try
            {
                this.RaiseListChangedEvents = false;
                foreach (var item in items.ToList())
                {
                    this.Remove(item);
                }
                this.OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1, -1));
            }
            finally
            {
                this.RaiseListChangedEvents = raiseListChangedEvents;
            }
        }
    }

    class ListChangedItemDeletedEventArgs : ListChangedEventArgs
    {
        public object Item { get; private set; }

        public ListChangedItemDeletedEventArgs(object item, int index)
            : base(ListChangedType.ItemDeleted, index, index)
        {
            this.Item = item;
        }
    }

    static class ListChangedEventArgsExtensions
    {
        /// <summary>
        /// Returns the deleted item if change type is ItemDeleted and BindingListEx is used, otherwise null.
        /// </summary>
        /// <returns>Null or the deleted item</returns>
        public static object GetDeletedItem(this ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemDeleted)
            {
                return ((ListChangedItemDeletedEventArgs)e).Item;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the deleted item if change type is ItemDeleted and BindingListEx is used, otherwise null.
        /// </summary>
        /// <returns>Null or the deleted item</returns>
        /// <exception cref="InvalidCastException" />
        public static T GetDeletedItem<T>(this ListChangedEventArgs e)
        {
            return (T)e.GetDeletedItem();
        }
    }
}
