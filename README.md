# SmartCommandlineArgs
A Visual Studio Extension which aims to provide a better UI to manage your command line arguments

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
![alt text](https://github.com/MBulli/SmartCommandlineArgs/blob/master/Doc/Images/AddIcon.png "Add Button"): Add new line  
![alt text](https://github.com/MBulli/SmartCommandlineArgs/blob/master/Doc/Images/RemoveIcon.png "Remove Button"): Remove selected lines  
![alt text](https://github.com/MBulli/SmartCommandlineArgs/blob/master/Doc/Images/MoveUpIcon.png "Move Up Button") / ![alt text](https://github.com/MBulli/SmartCommandlineArgs/blob/master/Doc/Images/MoveDownIcon.png "Move Down Button"): Move selected lines  
  
![alt text](https://github.com/MBulli/SmartCommandlineArgs/blob/master/Doc/Images/example.png "Commandline Arguments Window")


## Hotkeys
<kbd>CTRL</kbd>+<kbd>↑</kbd> / <kbd>CTRL</kbd>+<kbd>↓</kbd>: Move selected items.  
<kbd>Space</kbd>: Disable/Enable selected items.  
<kbd>Delete</kbd>: Remove selected items.  
<kbd>Insert</kbd>: Add a new item.  
<kbd>Alt</kbd>+<kbd>Enable/Disable Item</kbd>: Disable all other Items (useful if only one item should be enabled).
