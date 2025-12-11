using System.Text.Json.Serialization;

namespace PresensiQRBackend.Models
{
    public class LoginResponse
    {
        public LoginResponseData? Data { get; set; }
        public string? Message { get; set; }
        public int Result { get; set; }
        public bool Success { get; set; }
        public string? Timestamp { get; set; }
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }

    public class LoginResponseData
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        public string? Email { get; set; }
        [JsonPropertyName("personal_info")]
        public PersonalInfo? PersonalInfo { get; set; }
        [JsonPropertyName("role_id")]
        public int RoleId { get; set; }
    }

    public class PersonalInfo
    {
        public string? Birthday { get; set; }
        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }
        public int? Gender { get; set; }
        public string? Phone { get; set; }
        [JsonPropertyName("profile_picture")]
        public string? ProfilePicture { get; set; }
        public string? Username { get; set; }
    }
}