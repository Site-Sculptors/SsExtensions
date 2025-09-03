using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SsRefactor
{
    internal static class PropertyRegexHelper
    {
        public class PropertyInfo
        {
            public string Type { get; set; }
            public string FieldName { get; set; }
            public string PropertyName { get; set; }
            public string Kind { get; set; } // Observable, Auto, FullWithBacking, Full
            public List<string> DependentProperties { get; set; } = new List<string>();
            public string NoMatchReason { get; set; }
        }

        public static List<string> ExtractPropertyBlocks(string text)
        {
            var blocks = new List<string>();
            // Split by two or more newlines, which is a common separator between property blocks
            var candidates = Regex.Split(text, "(\r?\n){2,}");
            foreach (var candidate in candidates)
            {
                var trimmed = candidate.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                // Check if this candidate matches any property pattern
                if (MatchProperty(trimmed)?.NoMatchReason == null)
                {
                    blocks.Add(trimmed);
                }
            }
            return blocks;
        }

        public static string GetLeadingWhitespace(string text)
        {
            var match = Regex.Match(text, @"^(\s*)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public static PropertyInfo MatchProperty(string text)
        {
            // 1. Observable property
            var obsMatch = Regex.Match(text, @"\[ObservableProperty\]\s*private\s+([\w<>,\[\]\.\?]+)\s+_([\w_]+);", RegexOptions.Singleline);
            if (obsMatch.Success)
            {
                var type = obsMatch.Groups[1].Value;
                var field = "_" + obsMatch.Groups[2].Value;
                var propertyName = char.ToUpperInvariant(obsMatch.Groups[2].Value[0]) + obsMatch.Groups[2].Value.Substring(1);
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "Observable" };
            }

            // 2. Auto-property
            var autoMatch = Regex.Match(text, "(public|private|protected|internal)\\s+([\\w<>,\\[\\]\\.\\?]+)\\s+([\\w_]+)\\s*\\{\\s*get;\\s*set;\\s*\\}", RegexOptions.Singleline);
            if (autoMatch.Success)
            {
                var type = autoMatch.Groups[2].Value;
                var propertyName = autoMatch.Groups[3].Value;
                return new PropertyInfo { Type = type, PropertyName = propertyName, Kind = "Auto" };
            }

            // If not observable or auto, and is multi-line, try full property block matching
            if (text.Contains("\n") || text.Contains("\r"))
            {
                var fullProp = MatchFullPropertyBlock(text);
                if (fullProp != null)
                    return fullProp;
            }

            // No match
            return new PropertyInfo { NoMatchReason = "No recognizable property pattern found. Please select a valid property (auto, full, or observable)." };
        }

        // Matches full property blocks (with or without backing field)
        private static PropertyInfo MatchFullPropertyBlock(string text)
        {
            // 1. Full property with explicit backing field (field + property, SetProperty)
            var backingFieldMatch = Regex.Match(text,
                @"([a-zA-Z0-9_<>,\[\]\.\?]+)\s+([a-zA-Z0-9_]+)\s*=.*;\s*public\s+\1\s+([a-zA-Z0-9_]+)\s*\{[^}]*get\s*\{\s*return\s+([a-zA-Z0-9_]+);\s*\}[^}]*set\s*\{([^}]*)\}\s*\}",
                RegexOptions.Singleline);
            if (backingFieldMatch.Success)
            {
                var type = backingFieldMatch.Groups[1].Value;
                var field = backingFieldMatch.Groups[4].Value; // Use the field from the getter
                var propertyName = backingFieldMatch.Groups[3].Value;
                var setterBody = backingFieldMatch.Groups[5].Value;
                var dependentProperties = ExtractDependentProperties(setterBody, propertyName);
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "FullWithBacking", DependentProperties = dependentProperties };
            }

            // 2. Prism-style property: get { return myVar; } set { myVar = value; RaisePropertyChanged(); }
            var prismMatch = Regex.Match(text,
                @"(public|private|protected|internal)\s+([\w<>,\[\]\.\?]+)\s+([\w_]+)\s*\{\s*get\s*\{\s*return\s+([\w_]+);\s*\}\s*set\s*\{\s*\4\s*=\s*value;\s*RaisePropertyChanged\s*\(\s*\)\s*;?\s*\}\s*\}",
                RegexOptions.Singleline);
            if (prismMatch.Success)
            {
                var type = prismMatch.Groups[2].Value;
                var propertyName = prismMatch.Groups[3].Value;
                var field = prismMatch.Groups[4].Value;
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "PrismFullWithBacking" };
            }

            // 2b. INotifyPropertyChanged pattern: set { field = value; OnPropertyChanged(); }
            var notifyMatch = Regex.Match(text,
                @"(public|private|protected|internal)\s+([\w<>,\[\]\.\?]+)\s+([\w_]+)\s*\{\s*get\s*\{\s*return\s+([\w_]+);\s*\}\s*set\s*\{\s*\4\s*=\s*value;\s*OnPropertyChanged\s*\(\s*\)\s*;?\s*\}\s*\}",
                RegexOptions.Singleline);
            if (notifyMatch.Success)
            {
                var type = notifyMatch.Groups[2].Value;
                var propertyName = notifyMatch.Groups[3].Value;
                var field = notifyMatch.Groups[4].Value;
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "NotifyFullWithBacking" };
            }

            // 3. Expression-bodied get/set (e.g. get => isBusy; set => SetProperty(ref isBusy, value);)
            var exprMatch = Regex.Match(text, "(public|private|protected|internal)\\s+([\\w<>,\\[\\]\\.\\?]+)\\s+([\\w_]+)\\s*\\{\\s*get\\s*=>\\s*([\\w_]+);\\s*set\\s*=>\\s*SetProperty\\(ref\\s+([\\w_]+),\\s*value\\);\\s*\\}", RegexOptions.Singleline);
            if (exprMatch.Success)
            {
                var type = exprMatch.Groups[2].Value;
                var propertyName = exprMatch.Groups[3].Value;
                var field = exprMatch.Groups[4].Value;
                var dependentProperties = new List<string>();
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "FullWithBacking", DependentProperties = dependentProperties };
            }

            // 4. General full property (fallback, no backing field extraction)
            var fullMatch = Regex.Match(text, "(public|private|protected|internal)\\s+([\\w<>,\\[\\]\\.\\?]+)\\s+([\\w_]+)\\s*\\{[^}]*get[^}]*;?[^}]*set\\s*\\{([^}]*)\\}[^}]*\\}", RegexOptions.Singleline);
            if (fullMatch.Success)
            {
                var type = fullMatch.Groups[2].Value;
                var propertyName = fullMatch.Groups[3].Value;
                var setterBody = fullMatch.Groups[4].Value;
                var dependentProperties = ExtractDependentProperties(setterBody, propertyName);
                return new PropertyInfo { Type = type, PropertyName = propertyName, Kind = "Full", DependentProperties = dependentProperties };
            }

            return null;
        }

        // Extracts all property names from OnPropertyChanged calls in the setter body
        private static List<string> ExtractDependentProperties(string setterBody, string currentProperty)
        {
            var result = new List<string>();
            // Match OnPropertyChanged(nameof(...)) or OnPropertyChanged("...")
            var regex = new Regex(@"OnPropertyChanged\s*\(\s*(?:nameof\s*\(\s*([a-zA-Z0-9_]+)\s*\)|""([a-zA-Z0-9_]+)"")\s*\)");
            foreach (Match m in regex.Matches(setterBody))
            {
                var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (!string.IsNullOrEmpty(name) && name != currentProperty)
                    result.Add(name);
            }
            return result;
        }
    }
}
