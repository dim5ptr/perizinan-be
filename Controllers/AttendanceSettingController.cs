using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using System.Threading.Tasks;

namespace PresensiQRBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceSettingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AttendanceSettingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Endpoint untuk menampilkan semua setting presensi
        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            var settings = await _context.AttendanceSettings.ToListAsync();
            return Ok(settings);
        }

        // Endpoint untuk menampilkan setting terbaru saja
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSetting()
        {
            var current = await _context.AttendanceSettings
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();

            if (current == null)
                return NotFound(new { Message = "No settings found." });

            return Ok(current);
        }

        // Endpoint untuk update setting presensi
        [HttpPost("update")]
        public async Task<IActionResult> UpdateSetting([FromBody] AttendanceSetting request)
        {
            request.UpdatedAt = DateTime.UtcNow;
            _context.AttendanceSettings.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Attendance setting updated successfully." });
        }
    }
}
