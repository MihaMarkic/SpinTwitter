using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SpinTwitter.Models
{
    [DebuggerDisplay("{ReportDate} {Title}")]
    public class Value
    {
        [JsonPropertyName("wgsLat")]
        public decimal WgsLat { get; set; }
        [JsonPropertyName("wgsLon")]
        public decimal WgsLon { get; set; }

        [JsonPropertyName("prijavaCas")]
        public DateTimeOffset ReportDate { get; set; }

        [JsonPropertyName("obcinaNaziv")]
        public string? Municipality { get; set; }

        [JsonPropertyName("intervencijaVrstaNaziv")]
        public string? InterventionType { get; set; }

        [JsonPropertyName("dogodekNaziv")]
        public string? Caption { get; set; }

        [JsonPropertyName("besedilo")]
        public string? Text { get; set; }

        public string? Title => string.IsNullOrEmpty(Caption) ? InterventionType : Caption;
    }
}
