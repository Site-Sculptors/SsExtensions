using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using System.Text;

namespace SsRefactor
{
    internal sealed class ConvertToFullPropertyMenuCommand
    {
        public const int CommandId = 0x0112;
        public static readonly Guid CommandSet = new Guid("4E923D07-D7C1-4133-B05E-6AD24116262B");
        private readonly AsyncPackage package;

        private ConvertToFullPropertyMenuCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
            commandService?.AddCommand(menuItem);
        }

        public static ConvertToFullPropertyMenuCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToFullPropertyMenuCommand(package, commandService);
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
                if (PropertyRegexHelper.MatchProperty(selectedText) != null)
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
                var blocks = PropertyRegexHelper.ExtractPropertyBlocks(selectedText);
                var convertedFields = new List<string>();
                foreach (var block in blocks)
                {
                    var propInfo = PropertyRegexHelper.MatchProperty(block);
                    if (propInfo != null && propInfo.NoMatchReason == null)
                    {
                        var type = propInfo.Type;
                        var field = propInfo.FieldName ?? ("_" + char.ToLowerInvariant(propInfo.PropertyName[0]) + propInfo.PropertyName.Substring(1));
                        var propertyName = propInfo.PropertyName;
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"private {type} {field};");
                        sb.AppendLine($"public {type} {propertyName}");
                        sb.AppendLine("{");
                        sb.AppendLine($"    get => {field};");
                        sb.AppendLine($"    set => SetProperty(ref {field}, value);");
                        sb.AppendLine("}");
                        var output = sb.ToString().TrimEnd('\r', '\n');
                        var indent = PropertyRegexHelper.GetLeadingWhitespace(block);
                        var indentedOutput = string.Join("\n", output.Split('\n')).Replace("\n", "\n" + indent);
                        convertedFields.Add(indent + indentedOutput);
                    }
                }
                string finalOutput = string.Join("\n\n", convertedFields);
                if (!string.IsNullOrWhiteSpace(finalOutput))
                {
                    sel.Delete();
                    sel.Insert(finalOutput);
                }
                else
                {
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "Could not convert selected properties to full properties.",
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
    }
}
