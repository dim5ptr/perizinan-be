namespace PresensiQRBackend.Models
{
    public class FcmToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}