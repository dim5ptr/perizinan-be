using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresensiQRBackend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace PresensiQRBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> CheckHealth()
        {
            _logger.LogInformation("Menerima request CheckHealth pada {Timestamp}", DateTime.UtcNow);

            try
            {
                // Coba lakukan query sederhana ke database untuk memverifikasi koneksi
                await _context.Database.CanConnectAsync();
                var healthStatus = new
                {
                    status = "OK",
                    message = "Aplikasi berjalan dengan baik",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                _logger.LogInformation("CheckHealth berhasil: {Status}", healthStatus);
                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saat memeriksa kesehatan: {Message}", ex.Message);
                var healthStatus = new
                {
                    status = "Error",
                    message = $"Gagal memeriksa kesehatan: {ex.Message}",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                return StatusCode(500, healthStatus);
            }
        }
    }
}