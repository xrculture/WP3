using System.Text.Json.Serialization;

namespace Europeana3D.Web.Models
{
    public class EuropeanaSearchResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("items")] public List<EuropeanaSearchItem> Items { get; set; } = new();
        [JsonPropertyName("itemsCount")] public int ItemsCount { get; set; }
        [JsonPropertyName("totalResults")] public int TotalResults { get; set; }
    }

    public class EuropeanaSearchItem
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public List<string>? Title { get; set; }
        [JsonPropertyName("dataProvider")] public List<string>? DataProvider { get; set; }
        [JsonPropertyName("rights")] public List<string>? Rights { get; set; }
        [JsonPropertyName("edmPreview")] public List<string>? Preview { get; set; }


        // media links
        [JsonPropertyName("edmIsShownBy")] public List<string>? EdmIsShownBy { get; set; }
        [JsonPropertyName("edmIsShownAt")] public List<string>? EdmIsShownAt { get; set; }
        [JsonPropertyName("edmHasView")] public List<string>? EdmHasView { get; set; }
    }
}
