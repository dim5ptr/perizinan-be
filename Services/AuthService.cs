using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PresensiQRBackend.Models;

namespace PresensiQRBackend.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ssoApiUrl = "http://192.168.1.24:14041/api/sso/login.json";
        private readonly string _apiKey = "5af97cb7eed7a5a4cff3ed91698d2ffb";
        private readonly string _devKey = "12";
        private readonly ILogger<AuthService> _logger;
        private static readonly Dictionary<string, DateTime> _validTokens = new();
        private static readonly object _lock = new object();

        public AuthService(HttpClient httpClient, ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(string email, string password)

        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))

            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Email and password are required.",
                    Result = 0
                };
            }
            if (!email.Contains("@"))
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Silakan login menggunakan email, bukan username.",
                    Result = 0
                };
            }

            var userInfo = await AuthenticateAsync(email, password);
            if (userInfo == null)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Authentication failed.",
                    Result = 0
                };
            }

            _logger.LogDebug("LoginAsync: UserInfo.AccessToken = {AccessToken}", userInfo.AccessToken);

            // Store token with expiry
            lock (_lock)
            {
                var expiryTime = DateTime.UtcNow.AddHours(1); // Assume token expires in 1 hour
                _validTokens[userInfo.AccessToken] = expiryTime;
            }

            var loginResponse = new LoginResponse
            {
                Success = true,
                Message = "Login successful.",
                Result = 1,
                UserId = int.Parse(userInfo.UserId),
                Timestamp = DateTime.UtcNow.ToString("O"), // ISO 8601 format
                Data = new LoginResponseData
                {
                    AccessToken = userInfo.AccessToken,
                    Email = userInfo.Email,
                    RoleId = userInfo.RoleId,
                    PersonalInfo = new PersonalInfo
                    {
                        FullName = userInfo.UserName,
                        Username = email
                    }
                }
            };

            return loginResponse;
        }

        public async Task<LogoutResponse> LogoutAsync(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                return new LogoutResponse
                {
                    Success = false,
                    Message = "Authorization token is required."
                };
            }

            lock (_lock)
            {
                _validTokens.Remove(accessToken);
            }

            return new LogoutResponse
            {
                Success = true,
                Message = "Logout successful."
            };
        }

        private async Task<UserInfo?> AuthenticateAsync(string email, string password)

        {
            var requestBody = new
            {
                username = email,
                password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("dev-key", _devKey);

            var response = await _httpClient.PostAsync(_ssoApiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SSO API failed with status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("SSO API Response: {ResponseBody}", responseBody);

            var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("success", out var successElement) || !successElement.GetBoolean())
            {
                _logger.LogWarning("SSO API response does not contain 'success' or success is not true.");
                return null;
            }

            if (!root.TryGetProperty("data", out var userData))
            {
                _logger.LogWarning("SSO API response does not contain 'data'.");
                return null;
            }

            string userId = root.TryGetProperty("user_id", out var userIdElement) ? userIdElement.GetInt32().ToString() : "unknown";
            string accessToken = userData.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("SSO API response does not contain a valid 'access_token'.");
                return null;
            }

            _logger.LogDebug("AuthenticateAsync: Extracted AccessToken = {AccessToken}", accessToken);

            return new UserInfo
            {
                UserId = userId,
                Email = userData.TryGetProperty("email", out var emailElement) ? emailElement.GetString() ?? string.Empty : string.Empty,
                UserName = userData.TryGetProperty("personal_info", out var personalInfo) && personalInfo.TryGetProperty("full_name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                RoleId = userData.TryGetProperty("role_id", out var roleIdElement) ? roleIdElement.GetInt32() : 1,
                AccessToken = accessToken
            };
        }

        public bool ValidateAccessToken(string accessToken)
        {
            lock (_lock)
            {
                if (!_validTokens.TryGetValue(accessToken, out var expiryTime))
                    return false;

                if (DateTime.UtcNow > expiryTime)
                {
                    _validTokens.Remove(accessToken);
                    return false;
                }

                return true;
            }
        }

        internal string? GetUserNameFromToken(string accessToken)
        {
            throw new NotImplementedException();
        }
    }
}