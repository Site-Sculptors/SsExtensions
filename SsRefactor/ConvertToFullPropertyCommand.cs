using System.Text;

namespace SsRefactor
{
    internal class ConvertToFullPropertyCommand
    {
        // Converts any property (observable, auto, full, backing field) to a full property (MVVM pattern)
        public static string ConvertToFullProperty(string selectedText)
        {
            var propInfo = PropertyRegexHelper.MatchProperty(selectedText);
            if (propInfo == null)
                return null;

            var type = propInfo.Type;
            var field = propInfo.FieldName ?? ("_" + char.ToLowerInvariant(propInfo.PropertyName[0]) + propInfo.PropertyName.Substring(1));
            var propertyName = propInfo.PropertyName;

            var sb = new StringBuilder();
            sb.AppendLine($"private {type} {field};");
            sb.AppendLine($"public {type} {propertyName}");
            sb.AppendLine("{");
            sb.AppendLine($"    get => {field};");
            sb.AppendLine($"    set => SetProperty(ref {field}, value);");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
