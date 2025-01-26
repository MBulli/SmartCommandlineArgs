using Microsoft.VisualStudio.PlatformUI;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartCmdArgs.View
{
    public class GatherArgsQuestionDialog : DialogWindow
    {
        readonly SettingsViewModel settingsViewModel;

        private Guid? projectKind;

        public int ProjectCount { get; protected set; }

        public string ProjectKindName { get; protected set; }

        public GatherArgsQuestionDialog(SettingsViewModel settingsViewModel)
        {
            this.settingsViewModel = settingsViewModel;

            Width = 400;
            Height = 210;

            ResizeMode = System.Windows.ResizeMode.NoResize;

            Title = "Smart Commandline Arguments";

            var content = new GatherArgsQuestionControl();
            content.DataContext = this;
            Content = content;
        }

        public void DoClose(bool result, bool remember)
        {
            if (projectKind != null && remember)
            {
                if (projectKind == ProjectKinds.CPP)
                {
                    settingsViewModel.GatherArgsIgnoreCpp = result;
                }
            }

            this.DialogResult = result;
            this.Close();
        }

        public bool? ShowModal(Guid projectKind, int projectCount)
        {
            ProjectCount = projectCount;
            if (projectKind == ProjectKinds.CPP)
            {
                ProjectKindName = "C++";
            }

            this.projectKind = projectKind;
            return ShowModal();
        }
    }

    public class DesignGatherArgsQuestionDialog : GatherArgsQuestionDialog
    {
        public DesignGatherArgsQuestionDialog() : base(null)
        {
            ProjectCount = 20;
            ProjectKindName = "C++";
        }
    }
}
