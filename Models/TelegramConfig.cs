namespace PresensiQRBackend.Models
{
    public class TelegramConfig
    {
        public int Id { get; set; }
        public string ChatId { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAktif { get; set; } = true;

    }
}
