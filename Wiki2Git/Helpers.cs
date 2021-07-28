using System;
using System.Collections.Generic;
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
        public static string SanitizeFolderName(string name)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(name, "");
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
