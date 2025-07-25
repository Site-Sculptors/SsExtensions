using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsRefactor
{
    internal class ConvertToAutoPropertyCommand
    {
        // Converts any property (observable, auto, full, backing field) to an auto-property
        public static string ConvertToAutoProperty(string selectedText)
        {
            var propInfo = PropertyRegexHelper.MatchProperty(selectedText);
            if (propInfo == null)
                return null;
            return $"public {propInfo.Type} {propInfo.PropertyName} {{ get; set; }}";
        }
    }
}
