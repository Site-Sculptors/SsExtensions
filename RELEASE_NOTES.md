# SS Refactor Release Notes

## v1.2.2
- Tested on and compatible with both Visual Studio 2022 and Visual Studio 2026.
- 
## v1.2.0
- Improved property conversion robustness: all blocks are processed and preserved.
- Skipped properties are now reinserted with a prominent comment block.
- Properties that are commented out or unrecognized are never lost.
- Regex improvements for backing fields with/without initial values and for block/expression-bodied accessors.
- Enhanced user guidance and error handling.

## v1.1.2
- Added support for Prism-style and SetProperty properties with nullable types (e.g., bool?, int?, etc.).
- All property detection patterns now support nullable types.
- Internal refactoring for improved pattern matching and conversion reliability.

## v1.1.1
- Maintenance and documentation updates.
- Improved icon and installation troubleshooting guidance.

## v1.1.0
- Improved property detection: supports auto-properties, full properties (block-bodied, expression-bodied, SetProperty, Prism-style, etc.).
- Converts Prism-style properties (RaisePropertyChanged) and most MVVM patterns.
- Always generates fields with underscore prefix (e.g., _name).
- Automatically adds `using CommunityToolkit.Mvvm.ComponentModel;` at the top if missing.
- Checks for CommunityToolkit.Mvvm NuGet package and guides user to install if missing (with clipboard and NuGet UI support).
- Ensures containing class is partial, with prompt to make it partial if needed.
- Skips and warns about properties that cannot be safely converted (e.g., getter-only or complex logic).
- Improved error handling and user guidance.
- Compatible with .NET Framework 4.7.2 and Visual Studio 2022 or later.

## v1.0.0
- Initial release.
- Convert auto-properties and full properties to `[ObservableProperty]` fields.
- Context menu integration for quick access.
- Supports CommunityToolkit.Mvvm.
