using System.Text.Json.Serialization;

namespace SpinTwitter.Models
{
    public class Root
    {
        [JsonPropertyName("value")]
        public Value[] Value { get; set; } = new Value[0];
    }
}
