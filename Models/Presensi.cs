using System;

namespace PresensiQRBackend.Models
{
    public class Presensi
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string? IpAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? DeviceName { get; set; }
        public string? Status { get; set; } = "Hadir";
        public DateTime? CheckInTime { get; set; }
        public DateTime? BreakStartTime { get; set; }
        public DateTime? BreakEndTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? Token { get; set; }
    }
}