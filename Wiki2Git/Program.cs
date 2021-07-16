using System;
using System.IO;
using System.Linq;
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
            var url = "https://en.wikipedia.org/wiki/Hello_World";
            var fs = new FileStream(@"c:\devel\Wiki2Git\Wikipedia-20210716042420.xml", FileMode.Open);
            var outDirectory = @"c:\temp\_Wiki2Git";
            var serializer = new Serializer();
            var wiki = serializer.Deserialize<mediawiki>(fs);

            Directory.CreateDirectory(outDirectory);

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
                    var message = revision.comment + $"\n\n{url}?oldid={revision.id}&diff=prev";
                    Git($"config --local user.name \"{author}\"");
                    Git($"config --local user.email \"{authorId}@wikipedia.org\"");
                    Git($"commit --date=format:short:\"{revision.timestamp}\" --author=\"{author} <->\" -m \"{message}\" --allow-empty");
                }
            }
        }

        private static void Git(string arguments)
        {
            if (Helpers.Exec("git", arguments) != 0)
            {
                throw new Exception($"git {arguments} failed in {Directory.GetCurrentDirectory()}");
            }
        }
    }
}
