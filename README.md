# SS Refactor
<img src="SsRefactor/Resources/Images/icon.png" height="32" />

## Overview

**SS Refactor** from Site Sculptors is a Visual Studio extension that provides context menu commands to convert C# properties between MVVM patterns, including CommunityToolkit.Mvvm `[ObservableProperty]`, auto-properties, and full properties. It works with auto-properties, full properties, Prism-style properties, and most common MVVM property patterns.

## Features
- **Convert to ObservableProperty:** Converts auto-properties, full properties (block-bodied, expression-bodied, SetProperty, Prism-style, etc.) to `[ObservableProperty]` fields.
- **Convert to AutoProperty:** Converts full properties and observable fields to auto-properties (`{ get; set; }`).
- **Convert to FullProperty:** Converts auto-properties and observable fields to full MVVM properties using a backing field and `SetProperty` pattern.
- Context menu integration for quick access.
- Robust property detection for adjacent and variably formatted properties.
- Always generates fields with underscore prefix (e.g., `_name`).
- Automatically adds `using CommunityToolkit.Mvvm.ComponentModel;` if missing.
- Checks for CommunityToolkit.Mvvm NuGet package and guides user to install if missing (with clipboard and NuGet UI support).
- Ensures containing class is partial, with prompt to make it partial if needed.
- Skips and warns about properties that cannot be safely converted.
- Supports .NET Framework 4.7.2 and Visual Studio 2022 or later.

## Getting Started

1. **Install the Extension**
   - Download and install the VSIX from the [Releases page](https://github.com/Site-Sculptors/SsExtensions/releases) or from the Visual Studio Marketplace.
2. **Open Your C# Project** in Visual Studio 2022 (v17.0 or later).
3. **Highlight** one or more properties in your C# code (auto, full, or Prism-style).
4. **Right-click** to open the context menu.
5. Select one of the conversion commands:
   - **Convert to ObservableProperty**
   - **Convert to AutoProperty**
   - **Convert to FullProperty**
6. The selected properties will be replaced with the chosen property pattern.

## Example
**Before:**
```csharp
public string Name { get; set; }

private bool isBusy;
public bool IsBusy
{
    get => isBusy;
    set => SetProperty(ref isBusy, value);
}

private string myVar;
public string MyProperty
{
    get { return myVar; }
    set { myVar = value; RaisePropertyChanged(); }
}
```
**After (ObservableProperty):**
```csharp
[ObservableProperty]
private string _name;

[ObservableProperty]
private bool _isBusy;

[ObservableProperty]
private string _myProperty;
```

**After (AutoProperty):**
```csharp
public string Name { get; set; }
public bool IsBusy { get; set; }
public string MyProperty { get; set; }
```

**After (FullProperty):**
```csharp
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
// ...etc
```

## Requirements
- Visual Studio 2022 (v17.0 or later)
- .NET Framework 4.7.2 or later

## Installation

- Download and install the VSIX from the Visual Studio Marketplace or [GitHub Releases](https://github.com/Site-Sculptors/SsExtensions/releases)
- After installation, the extension should appear in the Extensions list with the SS Refactor icon. If the icon does not appear, see troubleshooting below.

## Troubleshooting: Missing Extension Icon

If the extension works but the icon is missing in the Extensions list or instructions:
- Ensure the icon file (`icon.png`) exists in `SsRefactor/Resources/Images/` and is referenced in the manifest.
- Try restarting Visual Studio.
- If the icon is still missing, uninstall and reinstall the extension.
- For more help, see [Report Issues](https://github.com/Site-Sculptors/SsExtensions/issues).

## Release Notes

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for full details (v1.1.0).

## Documentation & Support
- [GitHub Repository](https://github.com/Site-Sculptors/SsExtensions)
- [Report Issues](https://github.com/Site-Sculptors/SsExtensions/issues)

## License
MIT
