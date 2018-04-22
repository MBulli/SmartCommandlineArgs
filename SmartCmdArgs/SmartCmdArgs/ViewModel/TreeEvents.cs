﻿using System;
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
}