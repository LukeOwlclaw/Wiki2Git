using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wiki2Git
{
    public class GitterStory
    {
        private const string GitterStoryFileName = "story.json";

        public List<ArticleData> Articles { get; set; }

        public void Store()
        {
            var jsonString = JsonSerializer.Serialize(Articles, new JsonSerializerOptions { WriteIndented = true, });
            File.WriteAllText(GitterStoryFileName, jsonString, Encoding.UTF8);
        }

        public static GitterStory Load()
        {
            List<ArticleData>? articles = null;
            try
            {
                var jsonString = File.ReadAllText(GitterStoryFileName, Encoding.UTF8);
                articles = JsonSerializer.Deserialize<List<ArticleData>>(jsonString);
            }
            catch (Exception) { }

            if (articles == null) { articles = new List<ArticleData>(); }
            return new GitterStory
            {
                Articles = articles,
            };
        }

        private GitterStory()
        {
            Articles = new List<ArticleData>();
        }

    }
}
