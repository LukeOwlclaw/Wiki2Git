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
            var revisionList = new List<mediawikiPageRevision>();

            var exportUrls = new Dictionary<string, string> {
                {"en", "https://en.wikipedia.org/wiki/Special:Export" },
                {"de", "https://de.wikipedia.org/wiki/Special:Export" },
            };

            var outDirectory = @"c:\temp\_Wiki2Git";
            var pageName = "Berlin";

            var pageNameSane = Helpers.SanitizeFolderName(pageName);
            outDirectory = Path.Combine(outDirectory, pageNameSane);
            Directory.CreateDirectory(outDirectory);
            Directory.SetCurrentDirectory(outDirectory);

            Console.Write($"Dowloading Wikipedia article {pageName}... ");

            var baseList = new List<KeyValuePair<string?, string?>>
            {
                new KeyValuePair<string?, string?>("catname", ""),
                new KeyValuePair<string?, string?>("pages", pageName),
                //new KeyValuePair<string?, string?>("wpDownload", "1"),
                new KeyValuePair<string?, string?>("wpEditToken", "+\\"),
                new KeyValuePair<string?, string?>("title", "Special:Export"),
            };

            var tempFileTemplate = pageNameSane + "{0}.xml";
            string? lastTimeStamp = null;
            for (int pageCount = 0; pageCount < int.MaxValue; pageCount++)
            {
                var bodyList = baseList.ToList();
                if (lastTimeStamp != null)
                {
                    bodyList.Add(new KeyValuePair<string?, string?>("offset", lastTimeStamp));
                    Console.Write($"Dowloading offset {lastTimeStamp}... ");
                }

                var formContent = new FormUrlEncodedContent(bodyList);
                var outFile = string.Format(tempFileTemplate, pageCount);

                if (!File.Exists(outFile))
                {
                    using var client = new HttpClient();
                    var response = client.PostAsync(exportUrls["de"], formContent).Result;
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        using var fs2 = new FileStream(outFile, FileMode.CreateNew);
                        var s = response.Content.ReadAsStream();
                        s.Position = 0;
                        s.CopyTo(fs2);
                        Console.WriteLine($" saved {Helpers.FileSizeFormat(fs2.Position)} to {outFile}.");
                    }
                    else
                    {
                        throw new Exception($"Failed download for {pageName} at page {pageCount} ({lastTimeStamp})");
                    }
                }
                else
                {
                    Console.WriteLine($" file {outFile} already exists.");
                }

                var fs = new FileStream(outFile, FileMode.Open);
                var serializer = new Serializer();
                var wiki = serializer.Deserialize<mediawiki>(fs);

                revisionList.AddRange(wiki.page.Single().revision);

                if (wiki.page.Single().revision.Length < 1000)
                {
                    break;
                }

                lastTimeStamp = wiki.page.Single().revision.Last().timestamp;
            }

            var gitFolder = Path.Combine(outDirectory, "git");
            Directory.CreateDirectory(gitFolder);
            Directory.SetCurrentDirectory(gitFolder);
            if (!Directory.EnumerateDirectories(gitFolder).Any() &&
                !Directory.EnumerateFiles(gitFolder).Any())
            {
                Git("init");
            }

            var i = 0;
            foreach (var revision in revisionList)
            {
                if (i % 10 == 0)
                {
                    Console.WriteLine($"Git importing revision {i} of {revisionList.Count}...");
                }
                StoreGitRevision(pageName, revision);
                i++;
            }
        }

        private static string? lastAuthor = null;
        private static string? lastAuthorId = null;
        private static int? lastFileCounter = null;

        /// <summary>
        /// https://framagit.org/mdamien/wiki2git/blob/master/wiki2git
        /// </summary>
        /// <param name="pageName"></param>
        /// <param name="revision"></param>
        private static void StoreGitRevision(string pageName, mediawikiPageRevision revision)
        {
            RemoveOldFiles(revision.id);
            var fileCounter = 1;
            foreach (var text in revision.text)
            {
                var tryCount = 3;
                while (--tryCount >= 0)
                {
                    try
                    {
                        File.WriteAllText(pageName + fileCounter, text.Value);
                        break;
                    }
                    catch (IOException)
                    {
                        if (tryCount >= 0)
                        {
                            Console.WriteLine($"Failed to write {pageName + fileCounter}. tryCount:{tryCount}");
                            System.Threading.Thread.Sleep(1000);
                        }
                        else { throw; }
                    }
                }

                fileCounter++;
            }

            var addAllParameter = "--all";
            if (lastFileCounter != fileCounter)
            {
                Git("add *");
                addAllParameter = "";
            }

            lastFileCounter = fileCounter;

            var wikiUrl = "https://de.wikipedia.org/wiki/" + pageName;

            var author = revision.contributor.Single().username ?? revision.contributor.Single().ip;
            var authorId = revision.contributor.Single().id ?? revision.contributor.Single().ip;
            var message = SanitizeComment(revision.comment) + $"\n\n{wikiUrl}?oldid={revision.id}&diff=prev";
            if (author != lastAuthor)
            {
                Git($"config --local user.name \"{author}\"");
                lastAuthor = author;
            }

            if (authorId != lastAuthorId)
            {
                Git($"config --local user.email \"{authorId}@wikipedia.org\"");
                lastAuthorId = authorId;
            }

            Git($"commit {addAllParameter} --date=format:short:\"{revision.timestamp}\" --author=\"{author} <{authorId}@wikipedia.org>\" -m \"{message}\" --allow-empty");
        }

        private static void RemoveOldFiles(string id)
        {
            var tryCount = 3;
            while (--tryCount >= 0)
            {
                try
                {
                    string[] filePaths = Directory.GetFiles(Directory.GetCurrentDirectory());
                    foreach (string filePath in filePaths)
                    {
                        File.Delete(filePath);
                    }

                    break;
                }
                catch (IOException)
                {
                    if (tryCount >= 0)
                    {
                        Console.WriteLine($"Failed to cleanup {Directory.GetCurrentDirectory()} for {id}. tryCount:{tryCount}");
                        System.Threading.Thread.Sleep(1000);
                    }
                    else { throw; }
                }
            }
        }

        private static string SanitizeComment(string comment)
        {
            // ProcessStartInfo.Arguments:
            // To include quotation marks in the final parsed argument, triple-escape each mark.
            return comment?.Replace("\"", "\"\"\"") ?? "";
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
