using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (!(dropInfo.TargetItem is CmdArgument))
            {
                base.DragOver(dropInfo);
            }
        }
    }

    class DragHandler : DefaultDragHandler
    {
        public override bool CanStartDrag(IDragInfo dragInfo)
        {
            return !(dragInfo.SourceItem is CmdProject);
        }
    }

}
