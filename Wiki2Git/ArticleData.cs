using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki2Git
{
    public class ArticleData
    {
        public string? ArticleName { get; set; }
        public string? Language { get; set; }

        public string? Url { get; set; }

        public DateTime? LastImportDate { get; set; }
        public int StoredRevisions { get; set; }
    }
}
