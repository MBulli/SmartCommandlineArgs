using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1
{
    public class ListViewModel : PropertyChangedBase
    {
        private ObservableCollection<CmdProject> projects;

        public ObservableCollection<CmdProject> Projects { get => projects; }

        public IDropTarget DropHandler { get; } = new DropHandler();
        public IDragSource DragHandler { get; } = new DragHandler();


        public ListViewModel()
        {
            projects = new ObservableCollection<CmdProject>();
        }

    }

    class DropHandler : DefaultDropHandler
    {
        public override void DragOver(IDropInfo dropInfo)
        {
            // CmdArgument is not a DragTarget
            base.DragOver(dropInfo);
            if (dropInfo.TargetItem is CmdArgument)
            {
                if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter))
                {
                    dropInfo.DropTargetAdorner = null;
                    dropInfo.Effects = DragDropEffects.None;
                }
                else if (dropInfo.Effects != DragDropEffects.None)
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                }
            }
        }
    }

    class DragHandler : DefaultDragHandler
    {
        public override bool CanStartDrag(IDragInfo dragInfo)
        {
            return !((TreeViewItemEx)dragInfo.VisualSourceItem).ParentTreeView.SelectedItems.Cast<CmdBase>().Any(item => item is CmdProject);
        }

        public override void StartDrag(IDragInfo dragInfo)
        {
            var item = dragInfo.SourceItem as CmdBase;

            if (item?.IsEditable == true && item.IsInEditMode)
                item.CommitEdit();

            base.StartDrag(dragInfo);
        }
    }

}
