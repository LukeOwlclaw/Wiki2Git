using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

// Ignore spelling: Wikimedia Wikipedia
namespace Wiki2Git
{
    class Program
    {
        static void Main(string[] args)
        {
            // Download history of Wikimedia article via https://en.wikipedia.org/wiki/Special:Export
            // Create C# classes from XML: https://docs.microsoft.com/en-us/dotnet/standard/serialization/xml-schema-definition-tool-xsd-exe
            // > xsd.exe Wikipedia-20210716042420.xml
            // > xsd.exe Wikipedia-20210716042420.xsd /classes
            //var parser = new WikiParser();
            //var ser = new System.Xml.Serialization.XmlSerializer(typeof(mediawiki));

            var exportUrls = new Dictionary<string, string> {
                {"en", "https://en.wikipedia.org/wiki/Special:Export" },
                {"de", "https://de.wikipedia.org/wiki/Special:Export" },
            };

            var outDirectory = @"c:\temp\_Wiki2Git";
            Directory.CreateDirectory(outDirectory);
            Directory.SetCurrentDirectory(outDirectory);

            var pageName = "Berlin";

            var l = new List<KeyValuePair<string?, string?>>
            {
                new KeyValuePair<string?, string?>("catname", ""),
                new KeyValuePair<string?, string?>("pages", pageName),
                //new KeyValuePair<string?, string?>("wpDownload", "1"),
                new KeyValuePair<string?, string?>("wpEditToken", "+\\"),
                new KeyValuePair<string?, string?>("title", "Special:Export"),
            };
            var formContent = new FormUrlEncodedContent(l);

            //new KeyValuePair<string, string>("pages", url)
            //    new KeyValuePair<string, string>("wpEditToken", "+\\"),
            //    new KeyValuePair<string, string>("title", "Special:Export")


            var tempFile = "article.xml";
            File.Delete(tempFile);

            using (var client = new HttpClient())
            {
                var response = client.PostAsync(exportUrls["de"], formContent).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    using var fs2 = new FileStream(tempFile, FileMode.CreateNew);
                    var s = response.Content.ReadAsStream();
                    s.Position = 0;
                    s.CopyTo(fs2);
                    //File.WriteAllBytes("article.xml", );
                }
            }

            var fs = new FileStream(tempFile, FileMode.Open);

            var serializer = new Serializer();
            var wiki = serializer.Deserialize<mediawiki>(fs);

            if (wiki.page.First().revision.Length >= 1000)
            {
                var lastTimeStamp = wiki.page.First().revision.Last().timestamp;
                l.Add(new KeyValuePair<string?, string?>("offset", lastTimeStamp));
                formContent = new FormUrlEncodedContent(l);

                using (var client = new HttpClient())
                {
                    var response = client.PostAsync(exportUrls["de"], formContent).Result;
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        using var fs2 = new FileStream(tempFile, FileMode.CreateNew);
                        var s = response.Content.ReadAsStream();
                        s.Position = 0;
                        s.CopyTo(fs2);
                        //File.WriteAllBytes("article.xml", );
                    }
                }
            }

            // https://framagit.org/mdamien/wiki2git/blob/master/wiki2git
            foreach (var article in wiki.page)
            {
                Console.WriteLine(article.title);
                var subFolder = Helpers.SanitizeFolderName(article.title);
                var folder = Path.Combine(outDirectory, subFolder);
                Directory.CreateDirectory(folder);
                Directory.SetCurrentDirectory(folder);
                Git("init");

                foreach (var revision in article.revision)
                {
                    Console.WriteLine(revision);
                    //var sb = new StringBuilder();
                    var fileCounter = 1;
                    foreach (var text in revision.text)
                    {
                        File.WriteAllText("file" + fileCounter++, text.Value);
                    }

                    Git("add *");
                    var author = revision.contributor.Single().username ?? revision.contributor.Single().ip;
                    var authorId = revision.contributor.Single().id ?? revision.contributor.Single().ip;
                    var message = SanitizeComment(revision.comment) + $"\n\n{pageName}?oldid={revision.id}&diff=prev";
                    Git($"config --local user.name \"{author}\"");
                    Git($"config --local user.email \"{authorId}@wikipedia.org\"");
                    Git($"commit --date=format:short:\"{revision.timestamp}\" --author=\"{author} <->\" -m \"{message}\" --allow-empty");
                }
            }
        }

        private static string SanitizeComment(string comment)
        {
            // ProcessStartInfo.Arguments:
            // To include quotation marks in the final parsed argument, triple-escape each mark.
            return comment.Replace("\"", "\"\"\"");
        }

        private static void Git(string arguments)
        {
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            var ret = Helpers.Exec("git", arguments, sbOut, sbErr);
            if (ret != 0)
            {
                throw new Exception($"git {arguments} failed in {Directory.GetCurrentDirectory()}");
            }
        }
    }
}
