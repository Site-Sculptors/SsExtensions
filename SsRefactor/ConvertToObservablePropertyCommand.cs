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
using VSLangProj;
using System.Windows.Forms;

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

                // 1. Check for CommunityToolkit.Mvvm reference
                var project = dte.ActiveDocument?.ProjectItem?.ContainingProject;
                if (project != null && !ProjectHasMvvmToolkitReference(project))
                {
                    var result = VsShellUtilities.ShowMessageBox(
                        this.package,
                        "This project does not reference CommunityToolkit.Mvvm. Would you like to add it now?",
                        "SsRefactor",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    if (result == 6) // Yes
                    {
                        try
                        {
                            dte.ExecuteCommand("View.PackageManagerConsole");
                            dte.ExecuteCommand("NuGetPackageManagerConsole", "Install-Package CommunityToolkit.Mvvm");
                        }
                        catch
                        {
                            var openNuget = VsShellUtilities.ShowMessageBox(
                                this.package,
                                "Could not launch NuGet Package Manager Console. Please install CommunityToolkit.Mvvm manually.\n\nWould you like to open the NuGet Package Manager for this project now?",
                                "SsRefactor",
                                OLEMSGICON.OLEMSGICON_WARNING,
                                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                            if (openNuget == 6) // Yes
                            {
                                try
                                {
                                    System.Windows.Forms.Clipboard.SetText("CommunityToolkit.Mvvm");
                                    dte.ExecuteCommand("Project.ManageNuGetPackages");
                                    VsShellUtilities.ShowMessageBox(
                                        this.package,
                                        "The NuGet Package Manager is now open. Switch to the 'Browse' tab and paste (Ctrl+V) 'CommunityToolkit.Mvvm' into the search bar. The package name has been copied to your clipboard.",
                                        "SsRefactor",
                                        OLEMSGICON.OLEMSGICON_INFO,
                                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                                }
                                catch
                                {
                                    // If this fails, do nothing further
                                }
                            }
                            return;
                        }
                        // After attempting install, re-check reference
                        if (!ProjectHasMvvmToolkitReference(project))
                        {
                            var openNuget2 = VsShellUtilities.ShowMessageBox(
                                this.package,
                                "CommunityToolkit.Mvvm could not be added automatically. Please install it manually before converting to [ObservableProperty].\n\nWould you like to open the NuGet Package Manager for this project now?",
                                "SsRefactor",
                                OLEMSGICON.OLEMSGICON_WARNING,
                                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                            if (openNuget2 == 6) // Yes
                            {
                                try
                                {
                                    System.Windows.Forms.Clipboard.SetText("CommunityToolkit.Mvvm");
                                    dte.ExecuteCommand("Project.ManageNuGetPackages");
                                    VsShellUtilities.ShowMessageBox(
                                        this.package,
                                        "The NuGet Package Manager is now open. Switch to the 'Browse' tab and paste (Ctrl+V) 'CommunityToolkit.Mvvm' into the search bar. The package name has been copied to your clipboard.",
                                        "SsRefactor",
                                        OLEMSGICON.OLEMSGICON_INFO,
                                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                                }
                                catch
                                {
                                    // If this fails, do nothing further
                                }
                            }
                            return;
                        }
                    }
                    else
                    {
                        VsShellUtilities.ShowMessageBox(
                            this.package,
                            "CommunityToolkit.Mvvm must be installed before you can convert to [ObservableProperty].",
                            "SsRefactor",
                            OLEMSGICON.OLEMSGICON_WARNING,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        return;
                    }
                }

                // 2. Detect if containing class is not partial
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

                // Ensure using statement for CommunityToolkit.Mvvm.ComponentModel exists
                var doc = dte.ActiveDocument;
                var textDocObj = doc?.Object("TextDocument") as TextDocument;
                EditPoint docStart = textDocObj?.StartPoint.CreateEditPoint();
                EditPoint docEnd = textDocObj?.EndPoint.CreateEditPoint();
                string docText = docStart?.GetText(docEnd) ?? string.Empty;
                string usingStatement = "using CommunityToolkit.Mvvm.ComponentModel;";
                if (!docText.Contains(usingStatement))
                {
                    // Always insert at the very top
                    EditPoint insertPoint = textDocObj.StartPoint.CreateEditPoint();
                    insertPoint.Insert(usingStatement + "\n");
                }

                // Loop through all property blocks and convert each
                var blocks = PropertyRegexHelper.ExtractPropertyBlocks(selectedText);
                var convertedFields = new List<string>();
                foreach (var block in blocks)
                {
                    var propInfo = PropertyRegexHelper.MatchProperty(block);
                    if (propInfo != null && propInfo.NoMatchReason == null)
                    {
                        // Always use underscore prefix for field name
                        var fieldName = "_" + char.ToLowerInvariant(propInfo.PropertyName[0]) + propInfo.PropertyName.Substring(1);
                        var attributes = new List<string> { "[ObservableProperty]" };
                        foreach (var dep in propInfo.DependentProperties)
                        {
                            attributes.Add($"[NotifyPropertyChangedFor(nameof({dep}))]");
                        }
                        var output = string.Join("\n", attributes) + "\nprivate " + propInfo.Type + " " + fieldName + ";";
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

        // Helper: Check if project has CommunityToolkit.Mvvm reference
        private bool ProjectHasMvvmToolkitReference(EnvDTE.Project project)
        {
            var vsProject = project.Object as VSProject;
            if (vsProject != null)
            {
                foreach (Reference reference in vsProject.References)
                {
                    if (reference.Name == "CommunityToolkit.Mvvm")
                        return true;
                }
            }
            return false;
        }
    }
}
