namespace PresensiQRBackend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Nama { get; set; } = null!;
        public string AsalSekolah { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string NomorTelepon { get; set; } = null!;
        public DateTime TanggalLahir { get; set; }
        public string Gender { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string FotoProfil { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FcmToken { get; set; }
    }
}