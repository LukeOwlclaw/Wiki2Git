using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Wiki2Git
{
    public static class Helpers
    {
        public static int Exec(string fileName, string arguments, StringBuilder? outBuilder = null,
            StringBuilder? errBuilder = null)
        {
            var sb = new StringBuilder();
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            if (outBuilder != null)
            {
                process.StartInfo.RedirectStandardOutput = true;
                process.OutputDataReceived += (sender, data) =>
                {
                    outBuilder?.AppendLine(data.Data);
                };
            }

            if (errBuilder != null)
            {
                process.StartInfo.RedirectStandardError = true;
                process.ErrorDataReceived += (sender, data) =>
                {
                    errBuilder?.AppendLine(data.Data);
                };
            }

            process.Start();
            if (outBuilder != null) { process.BeginOutputReadLine(); }
            if (errBuilder != null) { process.BeginErrorReadLine(); }
            process.WaitForExit();
            return process.ExitCode;
        }

        private static HashSet<char>? gInvalidChars;

        public static HashSet<char> InvalidChars
        {
            get
            {
                if (gInvalidChars == null)
                {
                    gInvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()));
                }
                return gInvalidChars;
            }
        }

        /// <summary>
        /// Remove all invalid characters from <paramref name="fileOrFolderName"/> and returned sanitized form which can be used a folder/file name.
        /// </summary>
        /// <param name="fileOrFolderName">wanted folder/file name which potentially contains invalid characters</param>
        /// <returns>sanitized folder/file name </returns>
        internal static string? SanitizeFileAndFolderName(string fileOrFolderName)
        {
            if (string.IsNullOrEmpty(fileOrFolderName)) { return null; }
            var sb = new StringBuilder(fileOrFolderName.Length + 5);

            string valueFormD = fileOrFolderName.Normalize(NormalizationForm.FormD);

            foreach (var c in valueFormD)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    if (c == '*') { sb.Append("_STAR_"); }
                    else if (c == '¹') { sb.Append('1'); }
                    else if (c == '²') { sb.Append('2'); }
                    else if (c == '³') { sb.Append('3'); }
                    else if (c == '⁴') { sb.Append('4'); }
                    else if (c == '\'') { sb.Append('~'); }
                    else if (c == '"') { sb.Append('~'); }
                    else if (InvalidChars.Contains(c))
                    {
                        sb.Append('_');
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            return sb.ToString();
        }

        public static string FileSizeFormat(long sizeInBytes)
        {
            if (sizeInBytes < 0) { throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, "Size must be a non-negative number"); }
            decimal size = sizeInBytes;
            int index = 0;
            var units = new[] { "B", "KB", "MB", "GB", "TB" };

            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} {units[index]}";
        }
    }
}
