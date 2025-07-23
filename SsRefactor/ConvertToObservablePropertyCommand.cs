using System;
using System.Collections.Generic;
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
    internal sealed class ConvertToObservablePropertyCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("4E923D07-D7C1-4133-B05E-6AD24116262B");
        private readonly AsyncPackage package;

        private ConvertToObservablePropertyCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
            commandService?.AddCommand(menuItem);
        }

        public static ConvertToObservablePropertyCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToObservablePropertyCommand(package, commandService);
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
                if (HasAnyProperty(selectedText))
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

        // Returns true if at least one property is found in the selection
        private bool HasAnyProperty(string selectedText)
        {
            return GetPropertyMatches(selectedText).Count > 0;
        }

        // Finds all property matches (auto and full) in the selected text
        private List<Match> GetPropertyMatches(string selectedText)
        {
            var matches = new List<Match>();
            // Auto-properties
            var autoProps = Regex.Matches(selectedText, @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*\{\s*get;.*set;.*\}", RegexOptions.Singleline);
            foreach (Match m in autoProps) matches.Add(m);
            // Full properties with get/set blocks
            var fullProps = Regex.Matches(selectedText, @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*\{[^}]*get[^}]*;?[^}]*set[^}]*;?[^}]*\}", RegexOptions.Singleline);
            foreach (Match m in fullProps) matches.Add(m);
            return matches;
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
                string output = ConvertAllPropertiesToObservableFields(selectedText);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    sel.Delete();
                    sel.Insert(output);
                }
                else
                {
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "Could not convert selected properties to [ObservableProperty] fields.",
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

        // Converts all detected properties in the selection to ObservableProperty fields
        private string ConvertAllPropertiesToObservableFields(string selectedText)
        {
            var matches = GetPropertyMatches(selectedText);
            var fields = new List<string>();
            foreach (var match in matches)
            {
                if (match != null && match.Groups.Count >= 4)
                {
                    var type = match.Groups[2].Value;
                    var name = match.Groups[3].Value;
                    var field = "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
                    fields.Add("[ObservableProperty]" + Environment.NewLine + "private " + type + " " + field + ";");
                }
            }
            return string.Join(Environment.NewLine + Environment.NewLine, fields);
        }
    }
}
