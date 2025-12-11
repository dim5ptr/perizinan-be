using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using PresensiQRBackend.Services;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PresensiQRBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class IzinController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<IzinController> _logger;

        public IzinController(
            IPermissionService permissionService,
            ApplicationDbContext context,
            ILogger<IzinController> logger)
        {
            _permissionService = permissionService;
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateIzin(
            [FromForm] string namaPeminta,
            [FromForm] string jenisIzin,
            [FromForm] DateTime tanggalMulai,
            [FromForm] string alasan,
            [FromForm] string status,
            [FromForm] DateTime? tanggalSelesai,
            [FromForm] string? pembimbingChatId,
            [FromForm] string asalSekolah,
            [FromForm] DateTime tanggalPengajuan,
            [FromForm] IFormFile? foto)
        {
            try
            {
                _logger.LogInformation("Menerima request CreateIzin: namaPeminta={NamaPeminta}, jenisIzin={JenisIzin}, tanggalMulai={TanggalMulai}, alasan={Alasan}, status={Status}, tanggalSelesai={TanggalSelesai}, pembimbingChatId={PembimbingChatId}, tanggalPengajuan={TanggalPengajuan}, foto={FotoName}",
                    namaPeminta, jenisIzin, tanggalMulai, alasan, status, tanggalSelesai, pembimbingChatId, tanggalPengajuan, foto?.FileName);

                if (string.IsNullOrWhiteSpace(namaPeminta)) return BadRequest("Nama peminta wajib diisi.");
                if (string.IsNullOrWhiteSpace(jenisIzin)) return BadRequest("Jenis izin wajib diisi.");
                if (string.IsNullOrWhiteSpace(alasan)) return BadRequest("Alasan wajib diisi.");
                if (string.IsNullOrWhiteSpace(status)) return BadRequest("Status wajib diisi.");
                if (jenisIzin == "izin" && foto == null)
                    return BadRequest("Foto surat izin wajib diisi untuk jenis izin.");

                string nama = namaPeminta; // Gunakan namaPeminta langsung sebagai nama izin

                string year = DateTime.Now.ToString("yy");
                string month = DateTime.Now.ToString("MM");
                string prefix = $"{year}{month}";

                var lastIzin = await _context.Izins
                    .Where(i => i.KodeIzin.StartsWith(prefix))
                    .OrderByDescending(i => i.KodeIzin)
                    .FirstOrDefaultAsync();

                int nextNumber = 1;
                if (lastIzin != null && !string.IsNullOrEmpty(lastIzin.KodeIzin) && lastIzin.KodeIzin.Length >= 7)
                {
                    string lastNumStr = lastIzin.KodeIzin.Substring(4);
                    if (int.TryParse(lastNumStr, out int lastNum))
                    {
                        nextNumber = lastNum + 1;
                    }
                }

                string newKodeIzin = $"{prefix}{nextNumber.ToString("D3")}";

                string fotoBase64 = string.Empty;
                if (foto != null && foto.Length > 0)
                {
                    if (foto.Length > 5 * 1024 * 1024)
                        return BadRequest("File terlalu besar, maksimal 5MB.");
                    using (var ms = new MemoryStream())
                    {
                        await foto.CopyToAsync(ms);
                        fotoBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }

                var request = new IzinCreateRequest
                {
                    Nama = nama,
                    JenisIzin = jenisIzin,
                    TanggalMulai = DateTime.SpecifyKind(tanggalMulai, DateTimeKind.Utc),
                    Alasan = alasan,
                    Status = status,
                    TanggalSelesai = tanggalSelesai.HasValue
                        ? DateTime.SpecifyKind(tanggalSelesai.Value, DateTimeKind.Utc)
                        : (DateTime?)null,
                    FotoBase64 = fotoBase64,
                    TanggalPengajuan = DateTime.SpecifyKind(tanggalPengajuan, DateTimeKind.Utc),
                    PembimbingChatId = pembimbingChatId,
                    KodeIzin = newKodeIzin,
                };

                var result = await _permissionService.SubmitIzinAsync(request);
                Console.WriteLine("Data yang dikirim ke DB: " + JsonSerializer.Serialize(request));

                _logger.LogInformation("Izin berhasil dibuat: IzinId={IzinId}", result.GetType().GetProperty("IzinId")?.GetValue(result));
                return Ok(result);
            }
            catch (DbUpdateException ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error database saat simpan Izin: InnerException={InnerException}, Message={Message}", ex.InnerException?.Message, ex.Message);
                return StatusCode(500, $"Error database: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIzinStatus(int id, [FromBody] IzinStatusUpdate request)
        {
            _logger.LogInformation("Menerima request UpdateIzinStatus: IzinId={Id}, Status={Status}", id, request.Status);

            try
            {
                var izin = await _context.Izins.FirstOrDefaultAsync(i => i.Id == id);
                if (izin == null)
                {
                    _logger.LogWarning("Izin dengan ID {Id} tidak ditemukan.", id);
                    return NotFound(new { message = "Izin tidak ditemukan." });
                }

                // Validasi status yang diperbolehkan: Approved, Rejected, atau Revise
                if (request.Status != "Approved" && request.Status != "Rejected" && request.Status != "Revise")
                {
                    _logger.LogWarning("Status tidak valid: {Status}", request.Status);
                    return BadRequest(new { message = "Status harus Approved, Rejected, atau Revise." });
                }

                izin.Status = request.Status;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Status izin ID {Id} diperbarui menjadi {Status}.", id, request.Status);

                return Ok(new { 
                    message = $"Izin {request.Status}.",
                    id = id,
                    status = request.Status 
                });
            }
            catch (DbUpdateException ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error database saat update Izin: InnerException={InnerException}, Message={Message}", ex.InnerException?.Message, ex.Message);
                return StatusCode(500, $"Error database: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat update Izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpPut("{id}/revisi")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateIzinRevisi(int id,
            [FromForm] string? namaPeminta,
            [FromForm] string? jenisIzin ,
            [FromForm] DateTime? tanggalMulai,
            [FromForm] string? alasan,
            [FromForm] string? status,  // Ini opsional, tapi sebaiknya tidak diubah manual di revisi
            [FromForm] DateTime? tanggalSelesai,
            [FromForm] string? pembimbingChatId,
            [FromForm] IFormFile? foto)
        {
            _logger.LogInformation("Menerima request UpdateIzinRevisi: IzinId={Id}, namaPeminta={NamaPeminta}, jenisIzin={JenisIzin}, tanggalMulai={TanggalMulai}, alasan={Alasan}, status={Status}, tanggalSelesai={TanggalSelesai}, pembimbingChatId={PembimbingChatId}, foto={FotoName}",
                id, namaPeminta, jenisIzin, tanggalMulai, alasan, status, tanggalSelesai, pembimbingChatId, foto?.FileName);

            try
            {
                var izin = await _context.Izins.FirstOrDefaultAsync(i => i.Id == id);
                if (izin == null)
                {
                    _logger.LogWarning("Izin dengan ID {Id} tidak ditemukan.", id);
                    return NotFound(new { message = "Izin tidak ditemukan." });
                }

                // Validasi: Hanya izinkan revisi jika status saat ini "Revise"
                if (izin.Status != "Revise")
                {
                    _logger.LogWarning("Revisi hanya diperbolehkan untuk status 'Revise'. Status saat ini: {Status}", izin.Status);
                    return BadRequest(new { message = "Revisi hanya diperbolehkan jika status adalah 'Revise'." });
                }

                // Cek apakah ada perubahan minimal satu field
                bool hasChanges = !string.IsNullOrWhiteSpace(namaPeminta) ||
                                !string.IsNullOrWhiteSpace(jenisIzin) ||
                                tanggalMulai.HasValue ||
                                !string.IsNullOrWhiteSpace(alasan) ||
                                !string.IsNullOrWhiteSpace(status) ||
                                tanggalSelesai.HasValue ||
                                !string.IsNullOrWhiteSpace(pembimbingChatId) ||
                                (foto != null && foto.Length > 0);

                if (!hasChanges)
                {
                    _logger.LogWarning("Tidak ada perubahan yang diberikan untuk revisi Izin ID {Id}.", id);
                    return BadRequest(new { message = "Setidaknya satu field harus direvisi." });
                }

                // Perbarui hanya field yang diberikan (jika tidak null)
                if (!string.IsNullOrWhiteSpace(namaPeminta)) izin.Nama = namaPeminta;
                if (!string.IsNullOrWhiteSpace(jenisIzin)) izin.JenisIzin = jenisIzin;
                if (tanggalMulai.HasValue) izin.TanggalMulai = DateTime.SpecifyKind(tanggalMulai.Value, DateTimeKind.Utc);
                if (!string.IsNullOrWhiteSpace(alasan)) izin.Alasan = alasan;
                if (!string.IsNullOrWhiteSpace(status)) izin.Status = status;  // Opsional, tapi sebaiknya hindari ubah manual
                if (tanggalSelesai.HasValue) izin.TanggalSelesai = DateTime.SpecifyKind(tanggalSelesai.Value, DateTimeKind.Utc);
                if (!string.IsNullOrWhiteSpace(pembimbingChatId)) izin.PembimbingChatId = pembimbingChatId;

                // Handle unggah foto baru
                if (foto != null && foto.Length > 0)
                {
                    if (foto.Length > 5 * 1024 * 1024)
                        return BadRequest("File terlalu besar, maksimal 5MB.");
                    using (var ms = new MemoryStream())
                    {
                        await foto.CopyToAsync(ms);
                        izin.FotoBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }

                // Otomatis ubah status menjadi "Pending" setelah revisi
                izin.Status = "Pending";

                await _context.SaveChangesAsync();
                _logger.LogInformation("Izin ID {Id} berhasil direvisi dan status diubah menjadi 'Pending'.", id);

                return Ok(new { 
                    message = "Izin berhasil direvisi dan kembali ke status Pending.", 
                    id = izin.Id,
                    status = izin.Status 
                });
            }
            catch (DbUpdateException ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Error database saat revisi Izin: InnerException={InnerException}, Message={Message}", ex.InnerException?.Message, ex.Message);
                return StatusCode(500, $"Error database: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat revisi Izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIzinList()
        {
            _logger.LogInformation("Menerima request GetIzinList");

            try
            {
                var izinList = await _context.Izins
                    .GroupJoin(
                        _context.Pembimbing,
                        izin => izin.PembimbingChatId,
                        pembimbing => pembimbing.ChatId,
                        (izin, pembimbing) => new { izin, pembimbing }
                    )
                    .SelectMany(
                        x => x.pembimbing.DefaultIfEmpty(),
                        (x, pembimbing) => new
                        {
                            x.izin.Id,
                            x.izin.UserId,
                            x.izin.Nama,
                            x.izin.JenisIzin,
                            x.izin.TanggalMulai,
                            x.izin.TanggalSelesai,
                            x.izin.KodeIzin,
                            x.izin.Alasan,
                            x.izin.Status,
                            x.izin.TanggalPengajuan,
                            pembimbingChatId = pembimbing != null ? pembimbing.NamaPembimbing : null, // Mengembalikan NamaPembimbing sebagai pembimbingChatId
                            Foto = x.izin.FotoBase64 != null ? $"data:image/jpeg;base64,{x.izin.FotoBase64}" : null
                        })
                    .ToListAsync();

                _logger.LogInformation("Berhasil mengambil {Count} izin.", izinList.Count);
                return Ok(izinList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat mengambil daftar izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllIzin()
        {
            _logger.LogInformation("Menerima request GetAllIzin");

            try
            {
                var izinList = await _context.Izins
                    .GroupJoin(
                        _context.Pembimbing,
                        izin => izin.PembimbingChatId,
                        pembimbing => pembimbing.ChatId,
                        (izin, pembimbing) => new { izin, pembimbing }
                    )
                    .SelectMany(
                        x => x.pembimbing.DefaultIfEmpty(),
                        (x, pembimbing) => new
                        {
                            x.izin.Id,
                            x.izin.UserId,
                            Nama = x.izin.User != null ? x.izin.User.Nama : x.izin.Nama,
                            x.izin.JenisIzin,
                            x.izin.TanggalMulai,
                            x.izin.TanggalSelesai,
                            x.izin.KodeIzin,
                            x.izin.Alasan,
                            x.izin.Status,
                            x.izin.TanggalPengajuan,
                            pembimbingChatId = pembimbing != null ? pembimbing.NamaPembimbing : null, // Mengembalikan NamaPembimbing sebagai pembimbingChatId
                            Foto = x.izin.FotoBase64 != null ? $"data:image/jpeg;base64,{x.izin.FotoBase64}" : null
                        })
                    .ToListAsync();

                _logger.LogInformation("Berhasil mengambil {Count} izin.", izinList.Count);
                return Ok(izinList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat mengambil daftar izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet("riwayat/{userId}")]
        public async Task<IActionResult> GetRiwayatIzinByUserId(int userId)
        {
            _logger.LogInformation("Menerima request riwayat izin untuk UserId={UserId}", userId);

            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    _logger.LogWarning("UserId {UserId} tidak ditemukan.", userId);
                    return NotFound(new { message = "User tidak ditemukan." });
                }

                var riwayat = await _context.Izins
                    .Where(i => i.UserId == userId)
                    .GroupJoin(
                        _context.Pembimbing,
                        izin => izin.PembimbingChatId,
                        pembimbing => pembimbing.ChatId,
                        (izin, pembimbing) => new { izin, pembimbing }
                    )
                    .SelectMany(
                        x => x.pembimbing.DefaultIfEmpty(),
                        (x, pembimbing) => new
                        {
                            x.izin.Id,
                            x.izin.UserId,
                            x.izin.Nama,
                            x.izin.JenisIzin,
                            x.izin.TanggalMulai,
                            x.izin.TanggalSelesai,
                            x.izin.KodeIzin,
                            x.izin.Alasan,
                            x.izin.Status,
                            x.izin.TanggalPengajuan,
                            pembimbingChatId = pembimbing != null ? pembimbing.NamaPembimbing : null, // Mengembalikan NamaPembimbing sebagai pembimbingChatId
                            Foto = x.izin.FotoBase64 != null ? $"data:image/jpeg;base64,{x.izin.FotoBase64}" : null
                        })
                    .OrderByDescending(i => i.TanggalPengajuan)
                    .ToListAsync();

                _logger.LogInformation("Berhasil mengambil {Count} riwayat izin untuk UserId={UserId}.", riwayat.Count, userId);
                return Ok(riwayat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saat mengambil riwayat izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetIzinById(int id)
        {
            _logger.LogInformation("Menerima request GetIzinById untuk IzinId={Id}", id);

            try
            {
                var izin = await _context.Izins
                    .Where(i => i.Id == id)
                    .GroupJoin(
                        _context.Pembimbing,
                        izin => izin.PembimbingChatId,
                        pembimbing => pembimbing.ChatId,
                        (izin, pembimbing) => new { izin, pembimbing }
                    )
                    .SelectMany(
                        x => x.pembimbing.DefaultIfEmpty(),
                        (x, pembimbing) => new
                        {
                            x.izin.Id,
                            x.izin.UserId,
                            Nama = x.izin.Nama,
                            x.izin.JenisIzin,
                            x.izin.TanggalMulai,
                            x.izin.TanggalSelesai,
                            x.izin.KodeIzin,
                            x.izin.Alasan,
                            x.izin.Status,
                            x.izin.TanggalPengajuan,
                            pembimbingChatId = pembimbing != null ? pembimbing.NamaPembimbing : null, // Mengembalikan NamaPembimbing sebagai pembimbingChatId
                            Foto = x.izin.FotoBase64 != null ? $"data:image/jpeg;base64,{x.izin.FotoBase64}" : null
                        })
                    .FirstOrDefaultAsync();

                if (izin == null)
                {
                    _logger.LogWarning("Izin dengan ID {Id} tidak ditemukan.", id);
                    return NotFound(new { message = "Izin tidak ditemukan." });
                }

                _logger.LogInformation("Berhasil mengambil data izin untuk IzinId={Id}.", id);
                return Ok(izin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat mengambil data izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet("izinlist")]
        public async Task<IActionResult> GetIzinListWithoutPhoto()
        {
            _logger.LogInformation("Menerima request GetIzinListWithoutPhoto");

            try
            {
                var izinList = await _context.Izins
                    .GroupJoin(
                        _context.Pembimbing,
                        izin => izin.PembimbingChatId,
                        pembimbing => pembimbing.ChatId,
                        (izin, pembimbing) => new { izin, pembimbing }
                    )
                    .SelectMany(
                        x => x.pembimbing.DefaultIfEmpty(),
                        (x, pembimbing) => new
                        {
                            x.izin.Id,
                            x.izin.UserId,
                            x.izin.Nama, // Sama seperti GetIzinList()
                            x.izin.JenisIzin,
                            x.izin.TanggalMulai,
                            x.izin.TanggalSelesai,
                            x.izin.KodeIzin,
                            x.izin.Alasan,
                            x.izin.Status,
                            x.izin.TanggalPengajuan,
                            Pembimbing = pembimbing != null ? pembimbing.NamaPembimbing : null
                            // TIDAK ADA FOTO
                        })
                    .OrderByDescending(i => i.TanggalPengajuan)
                    .ToListAsync();

                _logger.LogInformation("Berhasil mengambil {Count} data izin tanpa foto.", izinList.Count);
                return Ok(izinList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat mengambil daftar izin tanpa foto: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }

        [HttpGet("foto/{id}")]
        public async Task<IActionResult> GetIzinPhoto(int id)
        {
            _logger.LogInformation("Menerima request GetIzinPhoto untuk IzinId={Id}", id);

            try
            {
                var fotoBase64 = await _context.Izins
                    .Where(i => i.Id == id)
                    .Select(i => i.FotoBase64)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(fotoBase64))
                {
                    _logger.LogWarning("Foto tidak ditemukan untuk IzinId={Id}", id);
                    return NotFound(new { message = "Foto tidak ditemukan atau izin tidak memiliki foto." });
                }

                var response = new
                {
                    id = id,
                    foto = $"data:image/jpeg;base64,{fotoBase64}"
                };

                _logger.LogInformation("Foto berhasil dikembalikan untuk IzinId={Id}", id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error server saat mengambil foto izin: {Message}", ex.Message);
                return StatusCode(500, $"Error server: {ex.Message}");
            }
        }
    }

    

    public class IzinStatusUpdate
    {
        public string? Status { get; set; }
    }
}