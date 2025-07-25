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

        public static PropertyInfo MatchProperty(string text)
        {
            // 1. Observable property
            var obsMatch = Regex.Match(text, @"\[ObservableProperty\]\s*private\s+([\w<>,\[\]\.]+)\s+_([\w_]+);", RegexOptions.Singleline);
            if (obsMatch.Success)
            {
                var type = obsMatch.Groups[1].Value;
                var field = "_" + obsMatch.Groups[2].Value;
                var propertyName = char.ToUpperInvariant(obsMatch.Groups[2].Value[0]) + obsMatch.Groups[2].Value.Substring(1);
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "Observable" };
            }

            // 2. Auto-property
            var autoMatch = Regex.Match(text, @"(public|private|protected|internal)\s+([\w<>,\[\]\.]+)\s+([\w_]+)\s*\{\s*get;\s*set;\s*\}", RegexOptions.Singleline);
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
            // Full property with backing field (field + property, SetProperty)
            var backingFieldMatch = Regex.Match(text,
                @"([a-zA-Z0-9_<>,\[\]\.]+)\s+([a-zA-Z0-9_]+)\s*=.*;\s*public\s+\1\s+([a-zA-Z0-9_]+)\s*\{[^}]*get\s*\{\s*return\s+\2;\s*\}[^}]*set\s*\{([^}]*)\}\s*\}",
                RegexOptions.Singleline);
            if (backingFieldMatch.Success)
            {
                var type = backingFieldMatch.Groups[1].Value;
                var field = backingFieldMatch.Groups[2].Value;
                var propertyName = backingFieldMatch.Groups[3].Value;
                var setterBody = backingFieldMatch.Groups[4].Value;
                var dependentProperties = ExtractDependentProperties(setterBody, propertyName);
                return new PropertyInfo { Type = type, FieldName = field, PropertyName = propertyName, Kind = "FullWithBacking", DependentProperties = dependentProperties };
            }

            // Full property (get/set blocks)
            var fullMatch = Regex.Match(text, @"(public|private|protected|internal)\s+([\w<>,\[\]\.]+)\s+([\w_]+)\s*\{[^}]*get[^}]*;?[^}]*set\s*\{([^}]*)\}[^}]*\}", RegexOptions.Singleline);
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
