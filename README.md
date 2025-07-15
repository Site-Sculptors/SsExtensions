# SS Refactor

![SS Refactor Icon](SsRefactor/Resources/Images/icon.png)

## Overview

**SS Refactor** is a Visual Studio extension that converts C# properties to CommunityToolkit.Mvvm `[ObservableProperty]` fields via the context menu. It works with both auto-properties and full properties.

## Features
- Convert auto-properties and full properties to `[ObservableProperty]` fields.
- Context menu integration for quick access.
- Supports CommunityToolkit.Mvvm.

## How to Use
1. **Highlight** an auto-property or full property in your C# code.
2. **Right-click** to open the context menu.
3. Select **Convert to ObservableProperty**.
4. The selected property will be replaced with an `[ObservableProperty]` field.

## Examples

### Auto-Property Example
**Before:**public string Name { get; set; }
**After:**
[ObservableProperty]
private string _name;
### Full Property Example
**Before:**private string _address;
public string Address 
{
    get { return _address; }
    set { _address = value; }
}
**After:**
[ObservableProperty]
private string _address;
## Requirements
- Visual Studio 2022 (v17.0 or later)
- .NET Framework 4.7.2 or later

## Installation
- Download and install the VSIX from the [Releases](https://github.com/BillyMartin1964/SsExtensions/releases) page or from the Visual Studio Marketplace.

## Documentation & Support
- [GitHub Repository](https://github.com/BillyMartin1964/SsExtensions)
- [Report Issues](https://github.com/BillyMartin1964/SsExtensions/issues)

## License
MIT
