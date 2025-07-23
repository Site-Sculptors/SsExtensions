using System;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace SsRefactor
{
    internal sealed class ConvertToRelayCommandCommand
    {
        public const int CommandId = 0x0110;
        public static readonly Guid CommandSet = new Guid("4E923D07-D7C1-4133-B05E-6AD24116262B");
        private readonly AsyncPackage package;

        private ConvertToRelayCommandCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
            commandService?.AddCommand(menuItem);
        }

        public static ConvertToRelayCommandCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToRelayCommandCommand(package, commandService);
        }

        private async void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
                return;
            menuCommand.Visible = false;
            menuCommand.Enabled = false;
            try
            {
                var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
                if (dte == null)
                    return;
                var textDoc = dte.ActiveDocument?.Object("TextDocument") as TextDocument;
                if (textDoc == null)
                    return;
                var sel = textDoc.Selection;
                if (sel == null || string.IsNullOrWhiteSpace(sel.Text))
                    return;
                string selectedText = sel.Text.Trim();
                if (IsRelayCommandCandidate(selectedText))
                {
                    menuCommand.Visible = true;
                    menuCommand.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MenuItem_BeforeQueryStatus: {ex.Message}");
            }
        }

        private bool IsRelayCommandCandidate(string selectedText)
        {
            // Enable for any code containing new AsyncRelayCommand, new RelayCommand, or new Command (generic or not)
            return Regex.IsMatch(selectedText, @"new\s+(Async)?RelayCommand(\s*<.*?>)?\s*\(", RegexOptions.Singleline)
                || Regex.IsMatch(selectedText, @"new\s+Command(\s*<.*?>)?\s*\(", RegexOptions.Singleline);
        }

        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;
                if (dte == null)
                    return;
                var textDoc = dte.ActiveDocument?.Object("TextDocument") as TextDocument;
                var sel = textDoc?.Selection;
                if (sel == null)
                    return;
                string selectedText = sel.Text.Trim();
                string output = TryConvertToRelayCommand(selectedText);
                if (output != null)
                {
                    sel.Delete();
                    sel.Insert(output);
                }
                else
                {
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "Could not convert selected command to RelayCommand.",
                        "SsRefactor",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Execute: {ex.Message}");
            }
        }

        private string TryConvertToRelayCommand(string selectedText)
        {
            string result = selectedText;

            // Replace 'new Command<T>' or 'new Command' with 'new RelayCommand<T>' or 'new RelayCommand'
            result = Regex.Replace(result, @"new\s+Command(<.*?>)?", "new RelayCommand$1");

            // Convert method group to lambda for RelayCommand and AsyncRelayCommand
            // Handles: new RelayCommand(TestMethod) => new RelayCommand(() => TestMethod())
            // Handles: new AsyncRelayCommand(TestMethod) => new AsyncRelayCommand(async () => await TestMethod())
            result = Regex.Replace(result,
                @"new\s+(Async)?RelayCommand(.*?)\((\s*)([A-Za-z_][A-Za-z0-9_]*)\s*\)(?!\s*=>)",
                m =>
                {
                    var asyncPrefix = m.Groups[1].Value;
                    var generic = m.Groups[2].Value;
                    var method = m.Groups[4].Value;
                    if (!string.IsNullOrEmpty(asyncPrefix))
                        return $"new AsyncRelayCommand{generic}(async () => await {method}())";
                    else
                        return $"new RelayCommand{generic}(() => {method}())";
                });

            // Convert lambda with missing parameter parentheses for AsyncRelayCommand
            // Handles: new AsyncRelayCommand<object>(async => TestMethod()) => new AsyncRelayCommand<object>(async () => TestMethod())
            result = Regex.Replace(result,
                @"new\s+AsyncRelayCommand(<.*?>)?\(\s*async\s*=>",
                m => $"new AsyncRelayCommand{m.Groups[1].Value}(async () =>");

            // Convert async lambda with parameter to async lambda with parameter (no change, but ensures consistency)
            // Handles: new AsyncRelayCommand<object>(async (parameter) => { })
            result = Regex.Replace(result,
                @"new\s+AsyncRelayCommand(<.*?>)?\(\s*async\s*\((.*?)\)\s*=>",
                m => $"new AsyncRelayCommand{m.Groups[1].Value}(async ({m.Groups[2].Value}) =>");

            return result != selectedText ? result : null;
        }
    }
}
