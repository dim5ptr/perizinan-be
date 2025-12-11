
namespace PresensiQRBackend.Models
{
    public class TokenRequest
    {
        public string? UserId { get; set; }
        public string? FcmToken { get; set; }
    }

    public class CheckInNotificationRequest
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime CheckInTime { get; set; }
    }

    public class PermissionNotificationRequest
    {
        internal string? PembimbingChatId;

        public int IzinId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? JenisIzin { get; set; }
        public DateTime TanggalMulai { get; set; }
        public string? Alasan { get; set; }
        public string? ChatId { get; set; } // New field for Telegram chat ID
    }

    public class PermissionStatusNotificationRequest
    {
        public int IzinId { get; set; }
        public string? UserId { get; set; }
        public bool IsApproved { get; set; }
    }
}
