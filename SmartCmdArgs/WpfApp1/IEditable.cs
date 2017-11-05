using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public interface IEditable : INotifyPropertyChanged
    {
        /// <summary>
        /// Returns whether the element is in edit mode or not
        /// </summary>
        bool IsInEditMode { get; }

        /// <summary>
        /// Puts the element into edit mode
        /// </summary>
        /// <param name="resetValue">If true the current value is discarded</param>
        void BeginEdit(bool resetValue = false);

        /// <summary>
        /// Commits any changes and ends the edit operation
        /// </summary>
        void EndEdit();
        /// <summary>
        /// Cancels the edit operation and discards any changes
        /// </summary>
        void CancelEdit();
    }
}
