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
            // Looks for a private ICommand field and a public property returning Command<T> or Command
            return Regex.IsMatch(selectedText, @"private\s+ICommand\s+[_\w]+;.*public\s+ICommand\s+\w+\s*=>\s*[_\w]+\s*\?\?=\s*new\s+Command<.*?>", RegexOptions.Singleline)
                || Regex.IsMatch(selectedText, @"private\s+ICommand\s+[_\w]+;.*public\s+ICommand\s+\w+\s*=>\s*[_\w]+\s*\?\?=\s*new\s+Command\s*\(", RegexOptions.Singleline);
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
            // Replace 'new Command<T>' or 'new Command' with 'new RelayCommand<T>' or 'new RelayCommand'
            string pattern = @"new\s+Command(<.*?>)?";
            string replacement = "new RelayCommand$1";
            string result = Regex.Replace(selectedText, pattern, replacement);

            // Convert method group to lambda: new RelayCommand(SomeMethod) => new RelayCommand(() => SomeMethod())
            // Only if not already a lambda or async lambda
            result = Regex.Replace(result,
                @"new\s+RelayCommand(.*?)\((\s*)([A-Za-z_][A-Za-z0-9_]*)\s*\)(?!\s*=>)",
                m => $"new RelayCommand{m.Groups[1].Value}(() => {m.Groups[3].Value}())");

            return result != selectedText ? result : null;
        }
    }
}
