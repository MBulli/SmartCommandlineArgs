using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.ViewModel
{
    public abstract class TreeEventBase
    {
        public CmdBase Sender { get; }
        public CmdProject AffectedProject { get; set; }

        public TreeEventBase(CmdBase sender)
        {
            this.Sender = sender;
        }
    }

    public abstract class GenericChangedEventArgs<TValue> : TreeEventBase
    {
        public TValue OldValue { get; }
        public TValue NewValue { get; }

        public GenericChangedEventArgs(CmdBase sender, TValue oldValue, TValue newValue)
            : base(sender)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class ParentChangedEvent : GenericChangedEventArgs<CmdContainer>
    {
        public ParentChangedEvent(CmdBase sender, CmdContainer oldParent, CmdContainer newParent)
            : base(sender, oldParent, newParent)
        {
        }
    }

    public class ValueChangedEvent : GenericChangedEventArgs<string>
    {
        public ValueChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class CheckStateChangedEvent : GenericChangedEventArgs<bool?>
    {
        public CheckStateChangedEvent(CmdBase sender, bool? oldValue, bool? newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class SelectionChangedEvent : GenericChangedEventArgs<bool>
    {
        public SelectionChangedEvent(CmdBase sender, bool oldValue, bool newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class ItemsChangedEvent : TreeEventBase
    {
        public NotifyCollectionChangedEventArgs ChangeEventArgs { get; }

        public ItemsChangedEvent(CmdBase sender, NotifyCollectionChangedEventArgs changeEventArgs)
            : base(sender)
        {
            ChangeEventArgs = changeEventArgs;
        }
    }

    public class ItemEditModeChangedEvent : TreeEventBase
    {
        public bool IsInEditMode { get; }

        public ItemEditModeChangedEvent(CmdBase sender, bool isInEditMode)
            : base(sender)
        {
            IsInEditMode = isInEditMode;
        }
    }

    public class ProjectConfigChangedEvent : GenericChangedEventArgs<string>
    {
        public ProjectConfigChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class ProjectPlatformChangedEvent : GenericChangedEventArgs<string>
    {
        public ProjectPlatformChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class LaunchProfileChangedEvent : GenericChangedEventArgs<string>
    {
        public LaunchProfileChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class ExclusiveModeChangedEvent : GenericChangedEventArgs<bool>
    {
        public ExclusiveModeChangedEvent(CmdBase sender, bool oldValue, bool newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class DelimiterChangedEvent : GenericChangedEventArgs<string>
    {
        public DelimiterChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class PrefixChangedEvent : GenericChangedEventArgs<string>
    {
        public PrefixChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class PostfixChangedEvent : GenericChangedEventArgs<string>
    {
        public PostfixChangedEvent(CmdBase sender, string oldValue, string newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class DefaultCheckedChangedEvent : GenericChangedEventArgs<bool>
    {
        public DefaultCheckedChangedEvent(CmdBase sender, bool oldValue, bool newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class ArgumentTypeChangedEvent : GenericChangedEventArgs<ArgumentType>
    {
        public ArgumentTypeChangedEvent(CmdArgument sender, ArgumentType oldValue, ArgumentType newValue)
            : base(sender, oldValue, newValue)
        {
        }
    }

    public class CheckStateWillChangeEvent : TreeEventBase
    {
        public CheckStateWillChangeEvent(CmdBase sender) : base(sender)
        {
        }
    }
}
