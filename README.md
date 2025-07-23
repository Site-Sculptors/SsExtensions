# SS Refactor
<img src="SsRefactor/Resources/Images/Banner.jpg" height="32" />

## Overview

**SS Refactor** is a Visual Studio extension that converts C# properties to CommunityToolkit.Mvvm `[ObservableProperty]` fields via the context menu. It works with both auto-properties and full properties.

## Features
- Convert auto-properties and full properties to `[ObservableProperty]` fields.
- Context menu integration for quick access.
- Supports CommunityToolkit.Mvvm.

## Getting Started

1. **Install the Extension**
   - Download and install the VSIX from the [Releases](https://github.com/BillyMartin1964/SsExtensions/releases) page or from the Visual Studio Marketplace.
2. **Open Your C# Project** in Visual Studio 2022 (v17.0 or later).
3. **Highlight** an auto-property or full property in your C# code.
4. **Right-click** to open the context menu.
5. Select **Convert to ObservableProperty**.
6. The selected property will be replaced with an `[ObservableProperty]` field.

## Example
**Before:**
```csharp
public string Name { get; set; }
```
**After:**
```csharp
[ObservableProperty]
private string _name;
```

## Requirements
- Visual Studio 2022 (v17.0 or later)
- .NET Framework 4.7.2 or later

## Release Notes

### v1.0.0
- Initial release.
- Convert auto-properties and full properties to `[ObservableProperty]` fields.
- Context menu integration.
- Support for CommunityToolkit.Mvvm.

## Documentation & Support
- [GitHub Repository](https://github.com/BillyMartin1964/SsExtensions)
- [Report Issues](https://github.com/BillyMartin1964/SsExtensions/issues)

## License
MIT
