using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartCmdArgs.ViewModel
{
    public class CmdGroup : CmdContainer
    {
        public override bool IsEditable => true;

        public new string ProjectConfig
        {
            get => base.ProjectConfig;
            set => base.ProjectConfig = value;
        }

        public new string ProjectPlatform
        {
            get => base.ProjectPlatform;
            set => base.ProjectPlatform = value;
        }

        public new string LaunchProfile
        {
            get => base.LaunchProfile;
            set => base.LaunchProfile = value;
        }

        public CmdGroup(Guid id, string name, IEnumerable<CmdBase> items, bool isExpanded, bool exclusiveMode, string projConf, string projPlatform, string launchProfile, string delimiter, string prefix, string postfix)
            : base(id, name, items, isExpanded, exclusiveMode, delimiter, prefix, postfix)
        {
            base.ProjectConfig = projConf;
            base.ProjectPlatform = projPlatform;
            base.LaunchProfile = launchProfile;
        }

        public CmdGroup(string name, IEnumerable<CmdBase> items = null, bool isExpanded = true, bool exclusiveMode = false, string projConf = null, string projPlatform = null, string launchProfile = null, string delimiter = " ", string prefix = "", string postfix = "")
            : this(Guid.NewGuid(), name, items, isExpanded, exclusiveMode, projConf, projPlatform, launchProfile, delimiter, prefix, postfix)
        { }

        public override CmdBase Copy()
        {
            return new CmdGroup(
                Value,
                Items.Select(cmd => cmd.Copy()),
                isExpanded,
                ExclusiveMode,
                ProjectConfig,
                ProjectPlatform,
                LaunchProfile,
                Delimiter,
                Postfix,
                Prefix);
        }
    }
}
