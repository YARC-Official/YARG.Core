using System.Globalization;
using System.Text;

namespace YARG.Core.Utility
{
    public enum CharacterGroup
    {
        Empty,
        AsciiSymbol,
        AsciiNumber,
        AsciiLetter,
        NonAscii
    }

    public static class StringTransformations
    {
        // Order of these static variables matters
        private static readonly (string, string)[] SearchLeniency =
        {
            ("Æ", "AE") // Tool - Ænema
        };

        private static readonly string[] Articles =
        {
            "the ", // The beatles, The day that never comes
            "el ",  // El final, El sol no regresa
            "la ",  // La quinta estacion, La bamba, La muralla verde
            "le ",  // Le temps de la rentrée
            "les ", // Les Rita Mitsouko, Les Wampas
            "los ", // Los fabulosos cadillacs, Los enanitos verdes,
        };

        public static string RemoveDiacritics(string? text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            foreach (var c in SearchLeniency)
            {
                text = text.Replace(c.Item1, c.Item2);
            }

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            unsafe
            {
                var buffer = stackalloc char[normalizedString.Length];
                int length = 0;
                foreach (char c in normalizedString)
                {
                    switch (CharUnicodeInfo.GetUnicodeCategory(c))
                    {
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.Format:
                        case UnicodeCategory.SpacingCombiningMark:
                            break;
                        default:
                            buffer[length++] = c;
                            break;
                    }
                }

                if (length < normalizedString.Length)
                {
                    normalizedString = new string(buffer, 0, length);
                }
                return normalizedString.ToLowerInvariant().Normalize(NormalizationForm.FormC);
            }
        }

        public static unsafe string RemoveUnwantedWhitespace(string arg)
        {
            var buffer = stackalloc char[arg.Length];
            int length = 0;
            int index = 0;
            while (index < arg.Length)
            {
                char curr = arg[index++];
                if (curr > 32)
                {
                    buffer[length++] = curr;
                }
                // length check accounts for pre-fix whitespace
                else if (length > 0 && index < arg.Length)
                {
                    curr = arg[index++];
                    if (curr > 32)
                    {
                        buffer[length++] = ' ';
                        buffer[length++] = curr;
                    }
                }
            }
            return length == arg.Length ? arg : new string(buffer, 0, length);
        }

        public static string RemoveArticle(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var article in Articles)
                {
                    if (StartsWith(name, article))
                    {
                        return name[article.Length..];
                    }
                }
            }
            return name;
        }

        // Why use a custom function versus .NET's built-in one? Because hot paths baby! YIPPEEEEEE!
        // Also, the use case is very controlled, so this won't hurt
        private static bool StartsWith(string str, string query)
        {
            int index = 0;
            if (str.Length >= query.Length)
            {
                while (index < query.Length && char.ToLowerInvariant(str[index]) == query[index])
                {
                    index++;
                }
            }
            return index == query.Length;
        }

        public static CharacterGroup GetCharacterGrouping(string str)
        {
            if (str.Length == 0)
            {
                return CharacterGroup.Empty;
            }

            return str[0] switch
            {
                >= 'a' and <= 'z' => CharacterGroup.AsciiLetter,
                >= '0' and <= '9' => CharacterGroup.AsciiNumber,
                _                 => str[0] > 127 ? CharacterGroup.NonAscii : CharacterGroup.AsciiSymbol
            };
        }
    }
}
