using System;
using System.ComponentModel.DataAnnotations;

namespace PresensiQRBackend.Models
{
    public class Izin
    {
        public User? User { get; set; }  // relasi ke tabel User (opsional, bisa diabaikan jika tidak digunakan)
        public string? PembimbingChatId { get; set; }

        public int Id { get; set; }
        public int UserId { get; set; } // Hapus [Required] untuk memungkinkan null
        public string Nama { get; set; } = "";
        public string FotoBase64 { get; set; } = string.Empty;
        [Required]
        public string Alasan { get; set; } = null!;
        public DateTime? TanggalMulai { get; set; }
        public DateTime? TanggalSelesai { get; set; }
        public string? Status { get; set; } = "Menunggu";
        public DateTime? TanggalPengajuan { get; set; } = DateTime.UtcNow;
        [Required]
        public string JenisIzin { get; set; } = string.Empty;
        public string KodeIzin { get; set; } = string.Empty;
    }
}