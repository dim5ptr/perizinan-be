namespace PresensiQRBackend.Models
{
    public class CheckInRequest
    {
        public string Token { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string MobileIpAddress { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
    }
}
