# SS Refactor Release Notes

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
