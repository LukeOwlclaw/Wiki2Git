using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Ignore spelling: config oldid de diff catname Wikipedia init prev
namespace Wiki2Git
{
    /// <summary>
    /// Downloads Wikipedia article and writes it to Git repository.
    /// Each Wikipedia revision will be converted to a Git commit.
    /// </summary>
    internal class WikiGitter
    {
        private readonly string mPageName;
        private readonly string mLanguage;
        private string mOutDirectory;

        /// <summary>
        /// Create instance of <see cref="WikiGitter"/>
        /// </summary>
        /// <param name="pageName">Wikipedia article name</param>
        /// <param name="language">Language of Wikipedia to use (only "en" and "de" supported;
        /// but likely any other language could be also used)</param>
        /// <param name="outDirectory">relative or absolute path to output directory.
        /// Within this directory a new directory using the <paramref name="pageName"/> will be created.</param>
        public WikiGitter(string pageName, string language, string outDirectory)
        {
            mPageName = pageName;
            mLanguage = language;
            mOutDirectory = outDirectory;

            if (!mWikiUrls.TryGetValue(mLanguage, out var _))
            {
                throw new ArgumentException($"Language {mLanguage} not supported. Available languages: {string.Join(", ", mWikiUrls.Keys)}");
            }
        }

        private Dictionary<string, string> mWikiUrls = new Dictionary<string, string> {
                {"en", "https://en.wikipedia.org/wiki/" },
                {"de", "https://de.wikipedia.org/wiki/" },
            };

        internal void Start(ref int startRevision)
        {
            var revisionList = new List<mediawikiPageRevision>();

            var pageNameSane = Helpers.SanitizeFolderName(mPageName);
            mOutDirectory = Path.Combine(mOutDirectory, pageNameSane);
            Directory.CreateDirectory(mOutDirectory);
            Directory.SetCurrentDirectory(mOutDirectory);
            // get absolute path
            mOutDirectory = Directory.GetCurrentDirectory();

            Console.Write($"Downloading Wikipedia article {mPageName}... ");

            var baseList = new List<KeyValuePair<string?, string?>>
            {
                //new KeyValuePair<string?, string?>("catname", ""),
                new KeyValuePair<string?, string?>("pages", mPageName),
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
                    Console.Write($"Downloading offset {lastTimeStamp}... ");
                }

                var formContent = new FormUrlEncodedContent(bodyList);
                var outFile = string.Format(tempFileTemplate, pageCount);

                if (!File.Exists(outFile))
                {
                    using var client = new HttpClient();
                    var response = client.PostAsync(mWikiUrls[mLanguage] + "Special:Export", formContent).Result;
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
                        throw new Exception($"Failed download for {mPageName} at page {pageCount} ({lastTimeStamp})");
                    }
                }
                else
                {
                    Console.WriteLine($" file {outFile} already exists.");
                }

                var fs = new FileStream(outFile, FileMode.Open);
                var serializer = new Serializer();
                var wiki = serializer.Deserialize<mediawiki>(fs);

                if (wiki.page == null || !(wiki.page.Any()))
                {
                    Console.WriteLine($"Page {mPageName} does not exist.");
                    return;
                }

                revisionList.AddRange(wiki.page.Single().revision);

                if (wiki.page.Single().revision.Length < 1000)
                {
                    break;
                }

                lastTimeStamp = wiki.page.Single().revision.Last().timestamp;
            }

            var gitFolder = Path.Combine(mOutDirectory, "git");
            Directory.CreateDirectory(gitFolder);
            Directory.SetCurrentDirectory(gitFolder);
            if (!Directory.EnumerateDirectories(gitFolder).Any() &&
                !Directory.EnumerateFiles(gitFolder).Any())
            {
                Git("init");
            }
            else if (startRevision == 0)
            {
                var sbOut = new StringBuilder();
                // Get log message of latest commit
                Git("log -1", ref sbOut);
                // e.g. https://de.wikipedia.org/wiki/Wissen?oldid=178452042&diff=prev
                var lastCommitMessage = sbOut.ToString();
                var searchWord = GetWikiRevisionLinkBase(mPageName);
                var start = lastCommitMessage.IndexOf(searchWord);
                if (start >= 0)
                {
                    start += searchWord.Length;
                    var end = lastCommitMessage.IndexOf('&', start);
                    if (end > start)
                    {
                        var revisionId = lastCommitMessage[start..end];
                        var index = revisionList.FindIndex(r => r.id == revisionId);
                        if (index > 0)
                        {
                            startRevision = index + 1;
                            Console.WriteLine($"Detected existing import. Continue after revision {revisionId} at index {startRevision}");
                        }

                    }
                }
            }

            var revisionListUse = revisionList.Skip(startRevision).ToList();
            foreach (var revision in revisionListUse)
            {
                if (startRevision % 10 == 0)
                {
                    Console.WriteLine($"Git importing revision {startRevision} of {revisionList.Count}...");
                }
                StoreGitRevision(mPageName, revision);
                startRevision++;
            }
        }

        private string? mLastAuthor = null;
        private string? mLastAuthorId = null;
        private int? mLastFileCounter = null;

        /// <summary>
        /// Write <paramref name="revision"/> of <paramref name="pageName"/> to Git repository.
        /// </summary>
        /// <param name="pageName">Wikipedia article name</param>
        /// <param name="revision">article revision</param>
        private void StoreGitRevision(string pageName, mediawikiPageRevision revision)
        {
            RemoveOldFiles(revision.id);
            var fileCounter = 0;
            foreach (var text in revision.text)
            {
                var tryCount = 3;
                while (--tryCount >= 0)
                {
                    try
                    {
                        if (text.Value != null)
                        {
                            var lines = SplitLines(text.Value);
                            File.WriteAllLines(pageName + fileCounter, lines);
                        }
                        else
                        {
                            // File was deleted
                            fileCounter--;
                        }

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
            if (mLastFileCounter != fileCounter)
            {
                Git("add *");
                addAllParameter = "";
            }

            mLastFileCounter = fileCounter;

            var author = SanitizeUserName(revision.contributor.Single().username)
                ?? revision.contributor.Single().ip;
            if (string.IsNullOrEmpty(author)) { author = "_no_author_"; }
            var authorId = revision.contributor.Single().id ?? revision.contributor.Single().ip;
            if (string.IsNullOrEmpty(authorId)) { authorId = "_no_authorid_"; }

            var wikiRevisionLink = GetWikiRevisionLink(pageName, revision);
            var message = SanitizeComment(revision.comment) + $"\n\n" + wikiRevisionLink;
            if (author != mLastAuthor)
            {
                Git($"config --local user.name \"{author}\"");
                mLastAuthor = author;
            }

            if (authorId != mLastAuthorId)
            {
                Git($"config --local user.email \"{authorId}@wikipedia.org\"");
                mLastAuthorId = authorId;
            }

            Git($"commit {addAllParameter} --date=format:short:\"{revision.timestamp}\" --author=\"{author} <{authorId}@wikipedia.org>\" -m \"{message}\" --allow-empty");
        }

        private string GetWikiRevisionLink(string pageName, mediawikiPageRevision revision)
        {
            var wikiRevisionLinkBase = GetWikiRevisionLinkBase(pageName);
            var wikiRevisionLink = $"{wikiRevisionLinkBase}{revision.id}&diff=prev";
            return wikiRevisionLink;
        }

        private string GetWikiRevisionLinkBase(string pageName)
        {
            var wikiUrl = mWikiUrls[mLanguage] + pageName;
            var wikiRevisionLinkBase = $"{wikiUrl}?oldid=";
            return wikiRevisionLinkBase;
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

        internal static string? SanitizeUserName(string username)
        {
            if (string.IsNullOrEmpty(username)) { return null; }
            var sb = new StringBuilder(username.Length + 5);

            string valueFormD = username.Normalize(NormalizationForm.FormD);

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

        /// <summary>
        /// Split <paramref name="value"/> into Git manageable chunks.
        /// </summary>
        /// <param name="value">input text</param>
        /// <returns>split text</returns>
        private IEnumerable<string> SplitLines(string value)
        {
            var builder = new StringBuilder((int)(value.Length * 1.05));
            const int lineLength = 80;
            using var reader = new StringReader(value);
            int c;
            int position = 0;
            bool breakAfterNextSpace = false;
            while ((c = reader.Read()) != -1)
            {
                if (breakAfterNextSpace && (c == ' ' || c == '\n'))
                {
                    var line = builder.ToString();
                    builder.Clear();
                    breakAfterNextSpace = false;
                    position = 0;
                    var lines = SplitLines(line, new[] { "|}|}", "<br>" });
                    if (lines != null)
                    {
                        foreach (var l in lines)
                        {
                            yield return l;

                        }
                    }
                    else
                    {
                        yield return line;
                    }
                }
                else
                {
                    builder.Append((char)c);

                    if (position >= lineLength) { breakAfterNextSpace = true; }
                    else
                    {
                        if (c == ','
                        || c == '.'
                        || c == '!'
                        || c == '?'
                        ) { breakAfterNextSpace = true; }
                    }
                }

                position++;
            }

            yield return builder.ToString();
        }

        /// <summary>
        /// Split <paramref name="input"/> after each <paramref name="keywords"/> and create empty line.
        /// </summary>
        /// <param name="input">input text</param>
        /// <param name="keywords">split words</param>
        /// <returns>split text</returns>
        private IEnumerable<string> SplitLines(string input, string[] keywords)
        {
            var builder = new StringBuilder(input.Length);
            var reader = new StringReader(input);
            int c;
            int[] matchPositionsOfKeywords = Enumerable.Repeat(0, keywords.Length).ToArray();
            while ((c = reader.Read()) != -1)
            {
                builder.Append((char)c);

                for (int i = 0; i < keywords.Length; i++)
                {
                    var nextMatchCharForI = keywords[i][matchPositionsOfKeywords[i]];
                    if (c == nextMatchCharForI)
                    {
                        matchPositionsOfKeywords[i]++;
                        if (matchPositionsOfKeywords[i] == keywords[i].Length)
                        {
                            // match keyWords[i]
                            matchPositionsOfKeywords[i] = 0;
                            var line = builder.ToString();
                            builder.Clear();
                            yield return line;
                            yield return string.Empty;
                        }
                    }
                    else
                    {
                        matchPositionsOfKeywords[i] = 0;
                    }
                }
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
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

        private static string SanitizeComment(string? comment)
        {
            if (comment == null) { return ""; }
            // ProcessStartInfo.Arguments:
            // To include quotation marks in the final parsed argument, triple-escape each mark.
            var sb = new StringBuilder(comment.Length + 5);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var c in comment)
                {
                    if (c == '"' || c == '&' || c == '|' || c == '(' || c == ')' || c == '<' || c == '>' || c == '^')
                    {
                        sb.Append('\\');
                    }

                    sb.Append(c);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var c in comment)
                {
                    if (c == '&' || c == '|' || c == '(' || c == ')' || c == '<' || c == '>' || c == '^')
                    {
                        sb.Append('^');
                    }
                    else if (c == '"') { sb.Append("\"\""); }

                    sb.Append(c);
                }
            }
            else { throw new NotSupportedException("OS platform not supported"); }

            return sb.ToString();
        }

        private static void Git(string arguments)
        {
            var ret = Helpers.Exec("git", arguments);
            if (ret != 0)
            {
                throw new Exception($"git {arguments} failed in {Directory.GetCurrentDirectory()}");
            }
        }

        private static void Git(string arguments, ref StringBuilder sbOut)
        {
            var ret = Helpers.Exec("git", arguments, sbOut);
            if (ret != 0)
            {
                throw new Exception($"git {arguments} failed in {Directory.GetCurrentDirectory()}");
            }
        }
    }
}
