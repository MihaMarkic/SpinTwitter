using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace SpinTwitter.Models
{
    [DebuggerDisplay("{ReportDate} {Title}")]
    public class Value
    {
        public decimal WgsLat { get; set; }
        public decimal WgsLon { get; set; }

        [JsonProperty(PropertyName = "prijavaCas")]
        public DateTimeOffset ReportDate { get; set; }

        [JsonProperty(PropertyName = "obcinaNaziv")]
        public string? Municipality { get; set; }

        [JsonProperty(PropertyName = "intervencijaVrstaNaziv")]
        public string? InterventionType { get; set; }

        [JsonProperty(PropertyName = "dogodekNaziv")]
        public string? Caption { get; set; }

        [JsonProperty(PropertyName = "besedilo")]
        public string? Text { get; set; }

        public string? Title => string.IsNullOrEmpty(Caption) ? InterventionType : Caption;
    }
}
