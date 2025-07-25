using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using System.Text.RegularExpressions;

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
            // Use PropertyRegexHelper to check for any property
            return PropertyRegexHelper.MatchProperty(selectedText) != null;
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

                // Detect if containing class is not partial
                var classText = GetContainingClassText(textDoc, sel);
                if (classText != null && !IsPartialClass(classText))
                {
                    var result = VsShellUtilities.ShowMessageBox(
                        this.package,
                        "The class must be declared as partial to use [ObservableProperty]. Would you like to make it partial?",
                        "SsRefactor",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    if (result == 6) // Yes
                    {
                        MakeClassPartial(textDoc, classText);
                    }
                    else
                    {
                        return;
                    }
                }

                // Loop through all property blocks and convert each
                var blocks = PropertyRegexHelper.ExtractPropertyBlocks(selectedText);
                var convertedFields = new List<string>();
                foreach (var block in blocks)
                {
                    var propInfo = PropertyRegexHelper.MatchProperty(block);
                    if (propInfo != null && propInfo.NoMatchReason == null)
                    {
                        var fieldName = propInfo.FieldName ?? ("_" + char.ToLowerInvariant(propInfo.PropertyName[0]) + propInfo.PropertyName.Substring(1));
                        var attributes = new List<string> { "[ObservableProperty]" };
                        foreach (var dep in propInfo.DependentProperties)
                        {
                            attributes.Add($"[NotifyPropertyChangedFor(nameof({dep}))]");
                        }
                        convertedFields.Add(string.Join("\n", attributes) + "\nprivate " + propInfo.Type + " " + fieldName + ";");
                    }
                }

                string output = string.Join("\n\n", convertedFields);
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

        // Helper: Get the text of the containing class
        private string GetContainingClassText(TextDocument textDoc, TextSelection sel)
        {
            var point = sel.ActivePoint;
            var editPoint = textDoc.CreateEditPoint();
            editPoint.MoveToPoint(point);
            // Search backwards for class declaration
            string classText = null;
            int linesToCheck = 50; // Reasonable limit
            for (int i = 0; i < linesToCheck && editPoint.Line > 1; i++)
            {
                editPoint.LineUp();
                var lineText = editPoint.GetLines(editPoint.Line, editPoint.Line + 1);
                if (Regex.IsMatch(lineText, @"class\s+[a-zA-Z0-9_]+"))
                {
                    classText = lineText;
                    break;
                }
            }
            return classText;
        }

        // Helper: Check if class is partial
        private bool IsPartialClass(string classText)
        {
            return Regex.IsMatch(classText, @"partial\s+class");
        }

        // Helper: Make class partial
        private void MakeClassPartial(TextDocument textDoc, string classText)
        {
            var editPoint = textDoc.StartPoint.CreateEditPoint();
            int totalLines = textDoc.EndPoint.Line;
            for (int i = 1; i <= totalLines; i++)
            {
                var lineText = editPoint.GetLines(i, i + 1);
                if (lineText.Contains(classText.Trim()))
                {
                    var newText = Regex.Replace(lineText, @"class", "partial class");
                    var replacePoint = textDoc.CreateEditPoint();
                    replacePoint.MoveToLineAndOffset(i, 1);
                    replacePoint.Delete(lineText.Length);
                    replacePoint.Insert(newText);
                    break;
                }
            }
        }
    }
}
