# Lönn Plugin Port Helper
A small tool that helps port your Ahorn Plugins to Lönn, though it doesn't automate the process (good luck writing a full julia -> lua converter)

# Notice
This tool is not made by the developers of Lönn, and they're not responsible for any bugs in this tool.

# Features
It currently has two features:
* Porting Ahorn .lang files to Lönn .lang files
* Porting Ahorn entity/trigger .jl plugins to Lönn .lua plugins
    
    Keep in mind that this isn't a full porting process - only the basic data about your entity are converted to Lönn's plugin format, you still have to handle rendering yourself.
    For that, check [Lönn's source code](https://github.com/CelestialCartographers/Loenn/tree/master/src/entities) and [Pandora's Box](https://github.com/Cruor/PandorasBox/tree/master/PandorasBox/Loenn) for official plugin examples.

# Usage
Download the [Release](https://github.com/JaThePlayer/LoennPluginPortHelper/releases) corresponding to your platform, and run the executable.
