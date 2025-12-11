using System.Text.Json.Serialization;

namespace PresensiQRBackend.Models
{
    public class LogoutResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}