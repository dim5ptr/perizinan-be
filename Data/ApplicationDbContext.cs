using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Models;

namespace PresensiQRBackend.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<FcmToken> FcmTokens { get; set; }
        public DbSet<Presensi> Presensis { get; set; }
        public DbSet<Izin> Izins { get; set; }
        public DbSet<AttendanceSetting> AttendanceSettings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Password> Passwords { get; set; }
        public DbSet<Pembimbing> Pembimbing { get; set; }
        public DbSet<TelegramConfig> TelegramConfigs { get; set; }
    }
}