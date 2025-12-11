namespace PresensiQRBackend.Models
{
    public class CreateTelegramConfigRequest
    {
        public string ChatId { get; set; }
        public string TopicId { get; set; }
        public string Description { get; set; }
    }
}
