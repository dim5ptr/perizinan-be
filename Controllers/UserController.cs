using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PresensiQRBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(ApplicationDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ────────────────────────────────
        // 1. Ambil user yang sedang login
        // ────────────────────────────────
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userIdStr = Request.Headers["X-User-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                {
                    _logger.LogWarning("X-User-Id header tidak ditemukan atau tidak valid.");
                    return Unauthorized("User tidak terautentikasi.");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound("User tidak ditemukan.");

                return Ok(new
                {
                    user.Id,
                    user.Nama,
                    user.AsalSekolah,
                    user.Username,
                    user.NomorTelepon,
                    user.TanggalLahir,
                    user.Gender,
                    user.Role,
                    user.FotoProfil,
                    user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetCurrentUser: {Message}", ex.Message);
                return StatusCode(500, "Error server: " + ex.Message);
            }
        }

        // ────────────────────────────────
        // 2. Ambil user berdasarkan ID (baru)
        // ────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var authIdStr = Request.Headers["X-User-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(authIdStr) || !int.TryParse(authIdStr, out var authId))
                {
                    _logger.LogWarning("X-User-Id header tidak ditemukan atau tidak valid.");
                    return Unauthorized("User tidak terautentikasi.");
                }

                var currentUser = await _context.Users.FindAsync(authId);
                if (currentUser == null)
                    return Unauthorized("Data user saat ini tidak valid.");

                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound("User tidak ditemukan.");

                // Validasi: Hanya admin (role 2) atau user itu sendiri yang boleh lihat
                if (currentUser.Role != "2" && authId != id)
                    return Unauthorized("Anda tidak diizinkan melihat profile ini.");

                return Ok(new
                {
                    user.Id,
                    user.Nama,
                    user.AsalSekolah,
                    user.Username,
                    user.NomorTelepon,
                    user.TanggalLahir,
                    user.Gender,
                    user.Role,
                    user.FotoProfil,
                    user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetUserById (ID: {Id}): {Message}", id, ex.Message);
                return StatusCode(500, "Error server: " + ex.Message);
            }
        }


        // ────────────────────────────────
        // 3. Update user + upload Base‑64
        // ────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto dto)
        {
            try
            {
                var authIdStr = Request.Headers["X-User-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(authIdStr) || !int.TryParse(authIdStr, out var authId))
                {
                    _logger.LogWarning("X-User-Id tidak valid.");
                    return Unauthorized("User tidak terautentikasi.");
                }

                var currentUser = await _context.Users.FindAsync(authId);
                if (currentUser == null)
                    return Unauthorized("Data user tidak valid.");

                _logger.LogInformation("🔍 authId: {authId}, role: {role}, targetId: {id}", authId, currentUser.Role, id);

                if ((currentUser.Role?.ToString() != "2") && authId != id)
                    return Unauthorized("Anda tidak diizinkan mengupdate user ini.");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { Errors = errors });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound("User tidak ditemukan.");

                if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
                    return BadRequest("Email tidak boleh diubah.");

                user.Nama = dto.Nama ?? user.Nama;
                user.AsalSekolah = dto.AsalSekolah ?? user.AsalSekolah;
                user.Username = dto.Username ?? user.Username;
                user.NomorTelepon = dto.NomorTelepon ?? user.NomorTelepon;
                user.Gender = dto.Gender ?? user.Gender;

                var tanggalValid = dto.TanggalLahir.HasValue && dto.TanggalLahir.Value.Year > 1900
                    ? DateTime.SpecifyKind(dto.TanggalLahir.Value, DateTimeKind.Utc)
                    : new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                user.TanggalLahir = tanggalValid;

                if (currentUser.Role == "2" && !string.IsNullOrEmpty(dto.Role))
                    user.Role = dto.Role;

                if (!string.IsNullOrWhiteSpace(dto.FotoBase64))
                {
                    try
                    {
                        var (mime, dataPart) = SplitBase64(dto.FotoBase64);
                        var ext = mime switch
                        {
                            "image/jpeg" => ".jpg",
                            "image/png" => ".png",
                            _ => null
                        };
                        if (ext == null) return BadRequest("File harus JPG/PNG.");

                        var bytes = Convert.FromBase64String(dataPart);
                        if (bytes.Length > 5 * 1024 * 1024)
                            return BadRequest("Ukuran maksimum 5 MB.");

                        // 🚫 HAPUS bagian penyimpanan ke file

                        // ✅ GANTI: Simpan langsung sebagai base64 string
                        user.FotoProfil = dto.FotoBase64;
                    }
                    catch (FormatException)
                    {
                        return BadRequest("Format foto tidak valid.");
                    }
                }

                await _context.SaveChangesAsync();


                return Ok(new
                {
                    user.Id,
                    user.Nama,
                    user.AsalSekolah,
                    user.Username,
                    user.NomorTelepon,
                    user.TanggalLahir,
                    user.Gender,
                    user.Role,
                    user.FotoProfil,
                    user.Email
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DB Error saat update user {Id}: {Message} | Inner: {Inner}", id, dbEx.Message, dbEx.InnerException?.Message);
                return StatusCode(500, "Gagal menyimpan data ke database. " + dbEx.InnerException?.Message ?? dbEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error UpdateUser ID {Id}: {Message}", id, ex.Message);
                return StatusCode(500, "Error server: " + ex.Message);
            }
        }

        // ────────────────────────────────
        // 4. Get all users (admin only?)
        // ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users.ToListAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal ambil daftar user: {Message}", ex.Message);
                return StatusCode(500, "Terjadi kesalahan pada server.");
            }
        }

        // ────────────────────────────────
        // Helper: pisahkan header & data Base‑64
        // ────────────────────────────────
        private static (string mime, string dataPart) SplitBase64(string input)
        {
            var comma = input.IndexOf(',');
            if (comma < 0) throw new FormatException("String Base64 tidak valid.");

            var header = input[..comma];
            var dataPart = input[(comma + 1)..];

            var mime = header
                .Replace("data:", "", StringComparison.OrdinalIgnoreCase)
                .Replace(";base64", "", StringComparison.OrdinalIgnoreCase);

            return (mime, dataPart);
        }
        [HttpPut("update-fcm-token")]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
        {
            try
            {
                _logger.LogInformation("Menerima permintaan update FCM token untuk UserId: {UserId}", request.UserId);

                if (string.IsNullOrEmpty(request.FcmToken))
                {
                    return BadRequest(new { message = "FCM Token tidak boleh kosong." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User dengan ID {UserId} tidak ditemukan.", request.UserId);
                    return NotFound(new { message = "User tidak ditemukan." });
                }

                user.FcmToken = request.FcmToken; // Asumsikan ada kolom FcmToken di tabel Users
                await _context.SaveChangesAsync();

                _logger.LogInformation("FCM Token berhasil diperbarui untuk UserId: {UserId}", request.UserId);
                return Ok(new { message = "FCM Token berhasil diperbarui.", userId = user.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saat memperbarui FCM token untuk UserId: {UserId}", request.UserId);
                return StatusCode(500, new { message = "Terjadi error internal server.", error = ex.Message });
            }
        }
    }

    public class UpdateFcmTokenRequest
    {
        public int UserId { get; set; }
        public string FcmToken { get; set; }
    }

    public class UserUpdateDto
    {
        public string? Nama { get; set; }
        public string? AsalSekolah { get; set; }
        public string? Username { get; set; }
        public string? NomorTelepon { get; set; }
        public DateTime? TanggalLahir { get; set; }
        public string? Gender { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public string? FotoBase64 { get; set; }
    }
}
