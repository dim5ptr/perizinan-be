using System;
using System.ComponentModel.DataAnnotations;

namespace PresensiQRBackend.Models
{
    public class AttendanceSetting
    {
        public int Id { get; set; }

        [Required]
        public TimeSpan CheckInDeadline { get; set; }   // contoh: 08:01

        [Required]
        public TimeSpan BreakStartTime { get; set; }    // contoh: 12:00

        [Required]
        public TimeSpan BreakEndTime { get; set; }      // contoh: 12:59

        [Required]
        public TimeSpan CheckOutTime { get; set; }      // contoh: 17:00

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
