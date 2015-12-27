//------------------------------------------------------------------------------
// <copyright file="ToolWindowControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace SmartCmdArgs.View
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for ToolWindowControl.
    /// </summary>
    public partial class ToolWindowControl : UserControl
    {
        public ViewModel.ToolWindowViewModel ViewModel
        {
            get { return (ViewModel.ToolWindowViewModel)DataContext; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindowControl"/> class.
        /// </summary>
        public ToolWindowControl()
        {
            this.InitializeComponent();
        }

        public ToolWindowControl(ViewModel.ToolWindowViewModel vm)
            : this()
        {
            DataContext = vm;
        }
    }
}