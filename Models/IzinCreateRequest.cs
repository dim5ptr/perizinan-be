namespace PresensiQRBackend.Models
{
    public class IzinCreateRequest
    {
        internal DateTime TanggalPengajuan;
        internal string? PembimbingChatId;

        public int UserId { get; set; }
        public string JenisIzin { get; set; } = "";
        public DateTime TanggalMulai { get; set; }
        public string Alasan { get; set; } = "";
        public string? FotoBase64 { get; set; }
        public string? Status { get; set; } // Changed from object
        public DateTime? TanggalSelesai { get; set; } // Changed from object
        public string? Nama { get; set; }

         public string KodeIzin { get; set; }
         
    }
}