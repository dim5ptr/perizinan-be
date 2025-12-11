namespace PresensiQRBackend.Models
{
    public class Password
    {
        public int Id { get; set; } // Primary key
        public int UserId { get; set; } // Foreign key ke User
        public string PasswordHash { get; set; } = null!; // Simpan hash password
        public User User { get; set; } = null!; // Navigasi ke User
    }
}