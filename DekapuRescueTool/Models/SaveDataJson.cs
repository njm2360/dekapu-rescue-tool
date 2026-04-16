using System.Text.Json.Serialization;

namespace DekapuRescueTool.Models;

public class SaveDataJson
{
    [JsonPropertyName("credit_all")] public string? CreditAll { get; set; }
    [JsonPropertyName("lastsave")] public string? LastSave { get; set; }
}
