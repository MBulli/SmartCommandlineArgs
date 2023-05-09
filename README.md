
# <img src="Doc/SmartCommandLineIcon-Readme.png">  Smart Command Line Arguments 
A Visual Studio Extension which aims to provide a better UI to manage your command line arguments and environment variables

> "The only smart way to pass standard command arguments to programs." - [A happy user](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments#review-details)

## Install

Install the extension inside Visual Studio or download it from the Visual Studio Marketplace:
- For VS 2022 go to [Smart Command Line Arguments VS2022](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments2022)
- For VS 2019 go to [Smart Command Line Arguments VS2019](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments)
- For VS 2017 go to [Smart Command Line Arguments VS2017](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments2017)
- For VS 2015 the last supported version is [v2.3.2](https://github.com/MBulli/SmartCommandlineArgs/releases/download/v2.3.2/SmartCmdArgs-v2.3.2.vsix)

Visual Studio 2017, 2019 and 2022 and the following project types are supported:
- C# .Net Framework
- C# .Net Core
- VB .Net
- F#
- C/C++
- Node.js
- Python
- Fortran
- Remote WSL Debugger
- Android Native Debugger
- Oasis NX Debugger

If you're using Cmake make sure to read the [Cmake support wiki page](https://github.com/MBulli/SmartCommandlineArgs/wiki/Cmake-support "Cmake").

## Usage
Open the window via:  
View → Other Windows → Commandline Arguments  

If the Window is open or minimized the commandline arguments should not be edited via the project properties.  
Changes which are made in the window are applied to all project configurations (Release/Debug etc.) of the current startup project.

## Interface
- ![Add button](Doc/Images/AddIcon.png "Add Button"): Add new line (Command Line Argument or Environment Variable)
- ![Remove button](Doc/Images/RemoveIcon.png "Remove Button"): Remove selected lines
- ![Add group button](Doc/Images/AddGroupIcon.png "Add Group Button"): Add new group
- ![Up/Down button](Doc/Images/MoveUpIcon.png "Move Up Button") / ![alt text](Doc/Images/MoveDownIcon.png "Move Down Button"): Move selected lines
- ![Copy cmd](Doc/Images/CopyCommandlineIcon.png "Copy commandline to clipboard"): Copy command line to clipboard. (Command Line Arguments or Environment Variables for Powershell or the Command Prompt)
- ![Show all projects](Doc/Images/ShowAllProjectsIcon.png "Show all Projects"): Toggle 'Show all projects' to also display non-startup projects.
- ![Settings](Doc/Images/SettingsIcon.png): Open the [Settings](#settings) dialog

![Window](Doc/Images/MainWindow.png "Command Line Arguments window, showning all projects")

## Settings
If the checkboxes are filled with a square the default value is used.
The default value for these settings can be configured under `Tools → Options → Smart Commandline Arguments → Settings Defaults`.

- **Save Settings to JSON**: If true then the these settings are stored in a JSON file next to the solution file.
- **Manage Command Line Arguments**: If enabled the arguments are set automatically when a project is started/debugged.
- **Manage Environment Variables**: If enabled the environment variables are set automatically when a project is started/debugged.
- **Enable version control support**: If enabled the extension will store the command line arguments into an json file at the same loctation as the related project file. That way the command line arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions `.suo-file` which is usally ignored by version control.
- **Use Solution Directory**: If enabled all arguments of every project will be stored in a single file next to the *.sln file. (Only if version control support is enabled)
- **Use Custom JSON Path**: If enabled the *.args.json file is saved at a custom location. Relative paths are based on the solution directory. (Only if 'Use Solution Directory' is enabled)
- **Enable Macro evaluation**: If enabled Macros like '$(ProjectDir)' will be evaluated and replaced by the corresponding string.

## Hotkeys
- <kbd>CTRL</kbd>+<kbd>↑</kbd> / <kbd>CTRL</kbd>+<kbd>↓</kbd>: Move selected items.
- <kbd>Space</kbd>: Disable/Enable selected items.
- <kbd>Delete</kbd>: Remove selected items.
- <kbd>Insert</kbd>: Add a new item.
- <kbd>Alt</kbd>+<kbd>Insert</kbd>: Add a new group.
- <kbd>Alt</kbd>+<kbd>Enable/Disable Item</kbd>: Disable all other Items (useful if only one item should be enabled).

## Paste

There are three ways to paste items into the list, drag'n'drop, <kbd>CTRL</kbd>-<kbd>V</kbd>, and the context menu.  
There are also three different types of data which can be pasted:
1. Prevously copied or cut items.  
2. Files, here a argument with the full file path is created for each file in the clipboard.
3. Text, where every line is a new argument. (Groups can also be represented, by a line ending with a `:`. Nested groups are done by indenting with a tab.)

## Context Menu

![Group Context Menu](Doc/Images/ContextMenuGroup.png "Context Menu with a single group selected")
![Item Context Menu](Doc/Images/ContextMenuItem.png "Context Menu with a single item selected")

- **Cut** / **Copy** / **Delete**: Cuts/Copies/Deletes the selected items.
- **Paste**: Pastes the previously copied/cut items, text, or files (see [Paste](#paste)).
- **CLA Delimiter** (only available while ONE group or project is selected): Options to set the string that each argument in the group is separated by. It can be set to a single space which is the default, none or something custom. If custom is selected you can also set a pre- and postfix that is prepend/append if the group is not empty.
- **Exclusive Mode** (only available while ONE group or project is selected): If this is checked then the group switches to a radio button mode where only one item can be checked at any given time.
- **New Group from Selection**: Creates a new Group and moves the selected items into it.
- **Split Argument** (only available while ONE argument is selected): Splits the argument with the typical cmd line parsing rules (e.g. `-o "C:\Test Folder\Output.png"` is split into two arguments `-o` and `"C:\Test Folder\Output.png"`).
- **Open/Reveal File/Folder** (only available while ONE item with a valid path is selected): If there is a file of folder name in the item then it can be opend or revealed with this option. (Base path for relative paths can be configured to project- or target path in the options)
- **Project Configuration** / **Launch Profile** / **Project Platform** (only available while ONE group is selected): Shows a sub menu to select a Project Configuration/Launch Profile/Project Platform. If this is set for a group, it is only evaluated if the right Project Configuration/Launch Profile/Project Platform is active.
- **Item Type** (only available while ONE item is selected): Switch the type of the item from command line argument to environment variable or back.
- **Set as single Startup Project** (only available while ONE project is selected): Sets the selected project as the startup project.
- **Reset to default checked**: Resets every selected arguemnt to the checked state given by the _Default Dhecked_ option.
- **Default Checked**: If this is checked then the argument sould be chekd by default e.g. if someone opens up the project for the first time.

## Donation
If you like this extension you can buy us a cup of coffee or a coke! :D

[![Donate via PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=FQWPXELLL26GS)

