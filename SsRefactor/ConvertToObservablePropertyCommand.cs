using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace SsRefactor
{
    internal sealed class ConvertToObservableCommand
    {
        public const int CommandId = 0x0100;

        public static readonly Guid CommandSet = new Guid("YOUR-GUID-HERE");

        private readonly AsyncPackage package;

        private ConvertToObservableCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService?.AddCommand(menuItem);
        }

        public static ConvertToObservableCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToObservableCommand(package, commandService);
        }

        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
            var textDoc = dte?.ActiveDocument?.Object("TextDocument") as TextDocument;
            var sel = textDoc?.Selection;

            if (sel == null)
                return;

            string selectedText = sel.Text.Trim();
            string output = TryParseToObservableField(selectedText);

            if (output != null)
            {
                sel.Delete();
                sel.Insert(output);
            }
            else
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "Could not convert selected property to [ObservableProperty].",
                    "SsRefactor",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private string TryParseToObservableField(string selectedText)
        {
            selectedText = selectedText.Trim();

            // Match auto-property
            var autoProp = Regex.Match(selectedText, @"public\s+([\w<>\[\]]+)\s+([\w_]+)\s*\{\s*get;\s*set;\s*\}");
            if (autoProp.Success)
            {
                var type = autoProp.Groups[1].Value;
                var name = autoProp.Groups[2].Value;
                var field = "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
                return $"[ObservableProperty]{Environment.NewLine}private {type} {field};";
            }

            // Match property with backing field and SetProperty usage
            var wrappedProp = Regex.Match(selectedText,
                @"private\s+([\w<>\[\]]+)\s+_([\w_]+);\s*public\s+\1\s+([\w_]+)\s*\{[^}]*get[^}]*=>[^;]*;[^}]*set[^}]*=>\s*SetProperty\s*\(\s*ref\s+_([\w_]+)\s*,\s*value\s*\)\s*;[^}]*\}",
                RegexOptions.Singleline);
            if (!wrappedProp.Success)
            {
                // Try to match block-bodied property with SetProperty in set accessor
                wrappedProp = Regex.Match(selectedText,
                    @"private\s+([\w<>\[\]]+)\s+_([\w_]+);\s*public\s+\1\s+([\w_]+)\s*\{[^}]*get[^}]*\{[^}]*return\s+_([\w_]+);[^}]*\}[^}]*set[^}]*\{[^}]*SetProperty\s*\(\s*ref\s+_([\w_]+)\s*,\s*value\s*\)\s*;[^}]*\}[^}]*\}",
                    RegexOptions.Singleline);
            }
            if (wrappedProp.Success)
            {
                var type = wrappedProp.Groups[1].Value;
                var field = wrappedProp.Groups[2].Value;
                return $"[ObservableProperty]{Environment.NewLine}private {type} _{field};";
            }

            return null;
        }
    }
}
