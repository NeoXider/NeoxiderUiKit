using System;

namespace Neo.UIKit
{
    /// <summary>
    /// Template over a label's original rich text. The importer wraps values as
    /// &lt;color=white&gt;&lt;gradient="..."&gt;512&lt;/gradient&gt;&lt;/color&gt;; updates must replace only the
    /// inner content of the gradient tag, preserving the wrapper.
    /// </summary>
    internal readonly struct UiRichText
    {
        private const string GradientOpen = "<gradient";
        private const string GradientClose = "</gradient>";

        /// <summary>Text before the replaceable content (includes the opening tags, if any).</summary>
        public readonly string Prefix;

        /// <summary>Text after the replaceable content (includes the closing tags, if any).</summary>
        public readonly string Suffix;

        /// <summary>Original inner content (e.g. "512" or "LEVEL 5").</summary>
        public readonly string Inner;

        private UiRichText(string prefix, string suffix, string inner)
        {
            Prefix = prefix;
            Suffix = suffix;
            Inner = inner;
        }

        /// <summary>Parses the original label text into a template around the gradient tag content.</summary>
        public static UiRichText Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new UiRichText(string.Empty, string.Empty, text ?? string.Empty);

            int open = text.IndexOf(GradientOpen, StringComparison.OrdinalIgnoreCase);
            if (open >= 0)
            {
                int innerStart = text.IndexOf('>', open);
                int innerEnd = text.IndexOf(GradientClose, open, StringComparison.OrdinalIgnoreCase);
                if (innerStart >= 0 && innerEnd > innerStart)
                {
                    innerStart++;
                    return new UiRichText(
                        text.Substring(0, innerStart),
                        text.Substring(innerEnd),
                        text.Substring(innerStart, innerEnd - innerStart));
                }
            }

            return new UiRichText(string.Empty, string.Empty, text);
        }

        /// <summary>Replaces the inner content, keeping the wrapper.</summary>
        public string Apply(string value)
        {
            return Prefix + value + Suffix;
        }

        /// <summary>
        /// Splits <see cref="Inner"/> around its last digit run ("LEVEL 5" → "LEVEL " + "5" + "").
        /// Returns false when the inner content contains no digits.
        /// </summary>
        public bool TrySplitNumber(out string numberPrefix, out string digits, out string numberSuffix)
        {
            numberPrefix = string.Empty;
            digits = string.Empty;
            numberSuffix = string.Empty;

            if (string.IsNullOrEmpty(Inner))
                return false;

            int end = -1;
            for (int i = Inner.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(Inner[i]))
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
                return false;

            int start = end;
            while (start > 0 && char.IsDigit(Inner[start - 1]))
                start--;

            numberPrefix = Inner.Substring(0, start);
            digits = Inner.Substring(start, end - start + 1);
            numberSuffix = Inner.Substring(end + 1);
            return true;
        }
    }
}
