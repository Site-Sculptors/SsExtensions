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
    internal sealed class ConvertToObservablePropertyCommand
    {
        // Using the same ID as defined in the VSCT file
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
            // Switch to the main thread - the call to AddCommand in the constructor requires the UI thread
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
                if (IsProperty(selectedText))
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

        private bool IsProperty(string selectedText)
        {
            try
            {
                // Debug output to help with troubleshooting
                System.Diagnostics.Debug.WriteLine($"Checking if text is property: {selectedText.Substring(0, Math.Min(50, selectedText.Length))}...");

                // Auto-property pattern (simpler version)
                if (Regex.IsMatch(selectedText, @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*\{\s*get;.*set;", RegexOptions.Singleline))
                {
                    System.Diagnostics.Debug.WriteLine("Auto-property pattern matched");
                    return true;
                }
                
                // Full property pattern with body blocks
                if (Regex.IsMatch(selectedText, @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*\{.*\bget\b.*\bset\b", RegexOptions.Singleline))
                {
                    System.Diagnostics.Debug.WriteLine("Full property with get/set blocks matched");
                    return true;
                }

                // Property with backing field pattern
                if (Regex.IsMatch(selectedText, @"(private|protected|internal)\s+([\w<>\[\]\.]+)\s+\w+.*?(public|private|protected|internal)\s+\2\s+\w+\s*\{", RegexOptions.Singleline))
                {
                    System.Diagnostics.Debug.WriteLine("Property with backing field matched");
                    return true;
                }

                // Expression-bodied property
                if (Regex.IsMatch(selectedText, @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*=>\s*", RegexOptions.Singleline))
                {
                    System.Diagnostics.Debug.WriteLine("Expression-bodied property matched");
                    return true;
                }
                
                System.Diagnostics.Debug.WriteLine("No property patterns matched");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IsProperty: {ex.Message}");
                return false;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Execute: {ex.Message}");
            }
        }

        private string TryParseToObservableField(string selectedText)
        {
            selectedText = selectedText.Trim();

            try
            {
                string type = null;
                string name = null;
                string backingFieldName = null;
                
                // Try to extract property name and type from various patterns
                
                // 1. Auto-property pattern
                var autoProp = Regex.Match(selectedText, 
                    @"(public|private|protected|internal)\s+([\w<>\[\]\.]+)\s+([\w_]+)\s*\{", 
                    RegexOptions.Singleline);
                if (autoProp.Success)
                {
                    type = autoProp.Groups[2].Value;
                    name = autoProp.Groups[3].Value;
                }
                
                // 2. Full property with backing field
                if (type == null)
                {
                    // Look for backing field first
                    var backingField = Regex.Match(selectedText, 
                        @"(private|protected|internal)\s+([\w<>\[\]\.]+)\s+(\w+)", 
                        RegexOptions.Singleline);
                    
                    if (backingField.Success)
                    {
                        type = backingField.Groups[2].Value;
                        backingFieldName = backingField.Groups[3].Value;
                        
                        // Try to find property name
                        var propertyDef = Regex.Match(selectedText, 
                            @"(public|private|protected|internal)\s+" + type + @"\s+([\w_]+)\s*\{", 
                            RegexOptions.Singleline);
                        
                        if (propertyDef.Success)
                        {
                            name = propertyDef.Groups[2].Value;
                        }
                    }
                }
                
                // If we found a type and name, construct the output
                if (type != null && name != null)
                {
                    // Create backing field name if we don't have one
                    if (backingFieldName == null)
                    {
                        backingFieldName = "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
                    }
                    else if (!backingFieldName.StartsWith("_") && backingFieldName[0] != '_')
                    {
                        // Ensure backing field starts with underscore for ObservableProperty convention
                        backingFieldName = "_" + backingFieldName;
                    }
                    
                    // Use simple string concatenation with Environment.NewLine to ensure proper formatting
                    return "[ObservableProperty]" + Environment.NewLine + 
                           "private " + type + " " + backingFieldName + ";";
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TryParseToObservableField: {ex.Message}");
                return null;
            }
        }
    }
}
