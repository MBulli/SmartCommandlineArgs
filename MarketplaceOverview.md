For VS 2022 go to [Smart Command Line Arguments VS2022](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments2022)

For VS 2019 go to [Smart Command Line Arguments VS2019](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments)

For VS 2017 go to [Smart Command Line Arguments VS2017](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments2017)

For VS 2015 the last supported version is [v2.3.2](https://github.com/MBulli/SmartCommandlineArgs/releases/download/v2.3.2/SmartCmdArgs-v2.3.2.vsix)

A Visual Studio Extension which aims to provide a better UI to manage your command line arguments, environment variables, working directory, and launch app.

![Main Window](vsix_preview_image.png "Command Line Arguments window, showning all projects")

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

If you're using Cmake make sure to read the [Cmake support wiki page](https://github.com/MBulli/SmartCommandlineArgs/wiki/Cmake-support "Cmake").

### Usage

Open the tool window via:  
View → Other Windows → Commandline Arguments

The extension must be enabled manually from within the extension window when opening a solution for the first time.
This behaviour can be chnaged via the [Options](#user-content-options).
The extension can be disabled via the [Settings](#user-content-settings).  
If the extions is enabled it controls command line arguments, environment variables, working directory, and/or the launch app depending on the 'Manage *' settings/options.
The project/launch configuration is changed every time items are changed in the extension window or the program is launched.
 
### Interface (Toolwindow Buttons)
- **Add**: Add new line (Command Line Argument or Environment Variable)
- **Remove**: Remove selected lines
- **Add group**: Add new group
- **Up/Down**: Move selected lines
- **Copy Cmd**: Copy command line to clipboard. (Command Line Arguments or Environment Variables for Powershell or the Command Prompt)
- **Show all projects**: Toggle to also display non-startup projects
- **Settings**: Open the [Settings](#user-content-settings) dialog

### Settings
If the checkboxes are filled with a square the default value is used.
The default value for these settings can be configured under [`Tools → Options → Smart Commandline Arguments → Settings Defaults`](#user-content-settings-defaults).

- **Save Settings to JSON**: If true then the these settings are stored in a JSON file next to the solution file.
- **Manage Command Line Arguments**: If enabled the arguments are set automatically when a project is started/debugged.
- **Manage Environment Variables**: If enabled the environment variables are set automatically when a project is started/debugged.
- **Manage Working Directories**: If enabled the working directories are set automatically when a project is started/debugged.
- **Manage Launch App**: If enabled the launch application is set automatically when a project is started/debugged.
- **Enable version control support**: If enabled the extension will store the command line arguments into an json file at the same loctation as the related project file. That way the command line arguments might be version controlled by a VCS. If disabled the extension will store everything inside the solutions `.suo-file` which is usally ignored by version control.
- **Use Solution Directory**: If enabled all arguments of every project will be stored in a single file next to the *.sln file. (Only if version control support is enabled)
- **Use Custom JSON Path**: If enabled the *.args.json file is saved at a custom location. Relative paths are based on the solution directory. (Only if 'Use Solution Directory' is enabled)
- **Enable Macro evaluation**: If enabled Macros like '$(ProjectDir)' will be evaluated and replaced by the corresponding string.
- **Disable Extension for Solution**: Disables the extion for the current solution. The extension can be enbaled again via the extension window.

## Options
Options can be found at `Tools → Options → Smart Commandline Arguments`.

### General
- **Enable Behaviour**: Controls if and how the extension enables itself by default. If the extension is disabled it can also be enabled via the extension window.
- **Relative path root**: Sets the base path that is used to resolve relative paths for the open/reveal file/folder context menu option.

### Appearance
- **Use Monospace Font**: If enabled the fontfamily is changed to 'Consolas'.
- **Display Tags for CLAs**: If enabled the item tag 'CLA' is displayed for Command Line Arguments. Normally tags are only displayed for environment variables (ENV), working directories (WD), and launch apps (APP).
- **Grey out inactive items**: Controls if items should be greyed out if are currently not applied.

### Cleanup
- **Delete empty files automatically**: Controls if empty '*.args.json' files will be delete automatically.

### Settings Defaults
This contols the default behaviour for [Settings](#user-content-settings)


### Hotkeys

Ctrl + ↑ / Ctrl + ↓: Move selected items.  
Space: Disable/Enable selected items.  
Delete: Remove selected items.  
Insert: Add a new item.  
Alt + Enable/Disable Item: Disable all other Items (useful if only one item should be enabled).

### Paste

There are three ways to paste items into the list, drag'n'drop, <kbd>CTRL</kbd>-<kbd>V</kbd>, and the context menu.  
There are also three different types of data which can be pasted:
1. Previously copied or cut items.  
2. Files, here a argument with the full file path is created for each file in the clipboard.
3. Text, where every line is a new argument. (Groups can also be represented, by a line ending with a `:`. Nested groups are done by indenting with a tab.)

### Context Menu

![Group Context Menu](https://raw.githubusercontent.com/MBulli/SmartCommandlineArgs/master/Doc/Images/ContextMenuGroup.png "Context Menu with a single group selected")
![Item Context Menu](https://raw.githubusercontent.com/MBulli/SmartCommandlineArgs/master/Doc/Images/ContextMenuItem.png "Context Menu with a single item selected")

- **Cut** / **Copy** / **Delete**: Cuts/Copies/Deletes the selected items.
- **Paste**: Pastes the previously copied/cut items, text, or files (see [Paste](#user-content-paste)).
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


### Donation
If you like this extension you can buy us a cup of coffee or a coke! :D

[![Donate via PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=FQWPXELLL26GS)
