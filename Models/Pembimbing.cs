namespace PresensiQRBackend.Models
{
    public class Pembimbing
    {
        public int Id { get; set; } // Primary key
        public string NamaPembimbing { get; set; } = null!; // Nama pembimbing
        public string ChatId { get; set; } = null!; // Chat ID untuk notifikasi
    }
}