using PresensiQRBackend.Models;

namespace PresensiQRBackend.Services
{
    internal class LoginData : LoginResponseData
    {
        public string AccessToken { get; set; }
        public string Email { get; set; }
        public int RoleId { get; set; }
    }
}