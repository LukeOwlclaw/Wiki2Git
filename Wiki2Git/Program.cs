using System;
using System.IO;

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
            var fs = new FileStream(@"c:\devel\Wiki2Git\Wikipedia-20210716042420.xml", FileMode.Open);
            var serializer = new Serializer();
            var wiki = serializer.Deserialize<mediawiki>(fs);
            foreach (var item in wiki.page)
            {
                Console.WriteLine(item);
            }
        }
    }
}
