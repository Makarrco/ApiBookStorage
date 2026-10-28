using System.Text.Json.Serialization;

namespace GutenbergApp
{
    public class GutenbergResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }  

        [JsonPropertyName("results")]
        public List<Book> Results { get; set; } = new();
    }

    public class Book
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("authors")]
        public List<Author> Authors { get; set; } = new();

        [JsonPropertyName("formats")]
        public Dictionary<string, string> Formats { get; set; } = new();

        public string AuthorsText => string.Join("; ", Authors.Select(a => a.Name));

        public string CoverUrl =>
            Formats.TryGetValue("image/jpeg", out var url) ? url : null;

        public string TextUrl =>
            Formats.FirstOrDefault(f => f.Key.StartsWith("text/plain")
                                     && !f.Value.EndsWith(".zip")).Value;
    }

    public class Author
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
