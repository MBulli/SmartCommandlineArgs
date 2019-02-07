
# <img src="Doc/SmartCommandLineIcon-Readme.png">  Smart Commandline Arguments 
A Visual Studio Extension which aims to provide a better UI to manage your command line arguments  

> "The only smart way to pass standard command arguments to programs." - [A happy user](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments#review-details)

## Install
Install the extension inside Visual Studio or download it from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MBulli.SmartCommandlineArguments "Visual Studio Marketplace").

Visual Studio 2015 and 2017 and the following project types are supported:
- C# .Net Framework
- C# .Net Core
- VB .Net
- F#
- C/C++ 
- Node.js
- Python (requires a nightly build of PTVS)

If you're using Cmake make sure to read the [Cmake support wiki page](https://github.com/MBulli/SmartCommandlineArgs/wiki/Cmake-support "Cmake").

## Usage
Open the window via:  
View → Other Windows → Commandline Arguments  
  
If the Window is open or minimized the commandline arguments should not be edited via the project properties.  
Changes which are made in the window are applied to all project configurations (Release/Debug etc.) of the current startup project.

## Version Control Support
The extension stores the commandline arguments inside a json file at the same location as the related project file.  
If this new behavior is not welcomed one can fallback to the 'old' mode where the commandline arguments have been stored inside the solutions .suo-file:  
Tools → Options → Smart Commandline Arguments → General → Enable version control support

## Interface
![Add button](Doc/Images/AddIcon.png "Add Button"): Add new line  
![Remove button](Doc/Images/RemoveIcon.png "Remove Button"): Remove selected lines  
![Up/Down button](Doc/Images/MoveUpIcon.png "Move Up Button") / ![alt text](Doc/Images/MoveDownIcon.png "Move Down Button"): Move selected lines  
![Copy cmd](Doc/Images/CopyCommandlineIcon.png "Copy commandline to clipboard"): Copy command line to clipboard. In the example below, the string `-f input_image.png -l latest.log -o out_image.png` is copied to the clipboard.
 
![Window](Doc/Images/example.png "Commandline Arguments Window")


## Hotkeys
<kbd>CTRL</kbd>+<kbd>↑</kbd> / <kbd>CTRL</kbd>+<kbd>↓</kbd>: Move selected items.  
<kbd>Space</kbd>: Disable/Enable selected items.  
<kbd>Delete</kbd>: Remove selected items.  
<kbd>Insert</kbd>: Add a new item.  
<kbd>Alt</kbd>+<kbd>Enable/Disable Item</kbd>: Disable all other Items (useful if only one item should be enabled).

## Donation
If you like this extension you can buy us a cup of coffee or a coke! :D

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.me/SmartCLIArgs/5)

