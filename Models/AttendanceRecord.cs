using System;
using System.ComponentModel.DataAnnotations;

namespace PresensiQRBackend.Models
{
    public enum RecordType
    {
        Attendance,
        Izin
    }

    public class AttendanceRecord
    {
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; } = null!;
        [Required]
        public RecordType RecordType { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? IpAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? DeviceName { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? BreakStartTime { get; set; }
        public DateTime? BreakEndTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? Token { get; set; }
        public string? FotoBase64 { get; set; }
        public string? Alasan { get; set; }
        public DateTime? TanggalMulai { get; set; }
        public DateTime? TanggalSelesai { get; set; }
        public string? Status { get; set; } = "Menunggu";
        public DateTime? TanggalPengajuan { get; set; } = DateTime.UtcNow;
    }
}