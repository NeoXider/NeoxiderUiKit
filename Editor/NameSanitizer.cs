using System.Collections.Generic;
using System.Text;

namespace Neo.UIKit.Editor
{
    /// <summary>
    /// Converts Figma/UXML element names into valid C# identifiers: PascalCase segments,
    /// invalid characters act as separators, a leading digit gets a "_" prefix, C# keywords
    /// are escaped with "@" and duplicates within one scope get a deterministic "_2" suffix.
    /// </summary>
    public static class NameSanitizer
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>Converts a raw element name to a PascalCase C# identifier.</summary>
        public static string ToPascalIdentifier(string name)
        {
            var sb = new StringBuilder();
            bool boundary = true;

            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if (char.IsLetterOrDigit(c))
                    {
                        sb.Append(boundary && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
                        boundary = false;
                    }
                    else
                    {
                        boundary = true;
                    }
                }
            }

            if (sb.Length == 0)
                return "_";

            string result = sb.ToString();
            if (char.IsDigit(result[0]))
                result = "_" + result;

            return Keywords.Contains(result) ? "@" + result : result;
        }

        /// <summary>
        /// Returns the identifier itself when unused, otherwise the first free "_2"/"_3"/... variant.
        /// The chosen name is added to <paramref name="used"/>.
        /// </summary>
        public static string Unique(string identifier, HashSet<string> used)
        {
            if (used.Add(identifier))
                return identifier;

            for (int i = 2; ; i++)
            {
                string candidate = identifier + "_" + i;
                if (used.Add(candidate))
                    return candidate;
            }
        }
    }
}
