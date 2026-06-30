using System;
using System.IO;

namespace YARG.Core.IO.Ini
{
    public static class SongIniWriter
    {
        public static void WriteSongOffset(string iniPath, long offset)
        {
            var lines = File.ReadAllLines(iniPath);

            bool inSongSection = false;
            bool foundDelay = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.Equals("[song]", StringComparison.OrdinalIgnoreCase))
                {
                    inSongSection = true;
                    continue;
                }

                if (inSongSection && trimmed.StartsWith("["))
                {
                    break;
                }

                if (inSongSection)
                {
                    int equalsIndex = trimmed.IndexOf('=');

                    if (equalsIndex > 0 &&
                        trimmed[..equalsIndex].Trim().Equals("delay", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"delay = {offset}";
                        foundDelay = true;
                        break;
                    }
                }
            }

            if (!foundDelay)
            {
                int insertIndex = Array.FindIndex(lines,
                    x => x.Trim().Equals("[song]", StringComparison.OrdinalIgnoreCase));

                if (insertIndex >= 0)
                {
                    var newLines = new string[lines.Length + 1];

                    Array.Copy(lines, 0, newLines, 0, insertIndex + 1);
                    newLines[insertIndex + 1] = $"delay = {offset}";
                    Array.Copy(lines, insertIndex + 1, newLines, insertIndex + 2, lines.Length - insertIndex - 1);

                    lines = newLines;
                }
            }

            File.WriteAllLines(iniPath, lines);
        }
    }
}