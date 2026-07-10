using System.Text;
using System.Text.Json.Serialization;

public class ContourPoint
{
    [JsonPropertyName("x")]
    public double X {get; set; }
    [JsonPropertyName("y")]
    public double Y {get; set; }
    [JsonPropertyName("kind")]
    public string Kind {get; set; } = "normal";
}