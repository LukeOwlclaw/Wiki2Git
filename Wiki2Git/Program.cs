using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using static CLAParser.CmdLineArgumentParser;

// Ignore spelling: de
namespace Wiki2Git
{
    class Program
    {
        static void Main(string[] args)
        {
            var clp = new CLAParser.CmdLineArgumentParser("Wiki2Git");
            clp.Parameter(ParamAllowType.Required, "", CLAParser.CmdLineArgumentParser.ValueType.String, "Wikipedia article");
            clp.Parameter(ParamAllowType.Required, "l", CLAParser.CmdLineArgumentParser.ValueType.String, "Language. Available: de, en");
            clp.Parameter(ParamAllowType.Optional, "o", CLAParser.CmdLineArgumentParser.ValueType.String, "Out path for git repository and downloaded article. Current directory if not set.");
            clp.Parameter(ParamAllowType.Optional, "r", CLAParser.CmdLineArgumentParser.ValueType.Int, "Start revision. For continuing earlier failed attempt.");
            try
            {
                clp.Parse();
            }
            catch (CmdLineArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(clp.GetUsage());
                Console.WriteLine(clp.GetParameterInfo());
                return;
            }

            var pageName = clp[""];
            var language = clp["l"];
            var outDirectory = clp["o"] ?? Directory.GetCurrentDirectory();
            int startRevision = Convert.ToInt32(clp["r"]);

            Console.CancelKeyPress += (sender, args) =>
            {
                Console.WriteLine($"Wiki2Git aborted for '{pageName}'. Continue at revision {startRevision}");
                Environment.Exit(1);
            };

            try
            {
                var wiki = new WikiGitter(pageName!, language!, outDirectory);
                wiki.Start(ref startRevision);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wiki2Git failed for '{pageName}'. Continue at revision {startRevision}");
                Console.WriteLine(ex);
            }
        }
    }
}
