        using Microsoft.AspNetCore.Mvc;
        using PresensiQRBackend.Data;
        using PresensiQRBackend.Models;
        using PresensiQRBackend.Services;
        using QRCoder;
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text.Json.Serialization;
        using System.Text.RegularExpressions;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.Configuration;

        namespace PresensiQRBackend.Controllers
        {
            [Route("api/[controller]")]
            [ApiController]
            public class AttendanceController : ControllerBase
            {
                private readonly ApplicationDbContext _context;
                private readonly AuthService _authService;
                private readonly INotificationService _notificationService;
                private static readonly Dictionary<string, DateTime> _activeTokens = new();
                private static readonly object _lock = new object();
                private readonly TimeZoneInfo _jakartaTimeZone;

                public AttendanceController(ApplicationDbContext context, AuthService authService, INotificationService notificationService, IConfiguration configuration)
                {
                    _context = context;
                    _authService = authService;
                    _notificationService = notificationService;

                    try
                    {
                        _jakartaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        try
                        {
                            _jakartaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
                        }
                        catch (TimeZoneNotFoundException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error: Jakarta timezone not found. Falling back to UTC. Error: {ex.Message}");
                            _jakartaTimeZone = TimeZoneInfo.Utc;
                        }
                    }
                }

                [HttpGet("generate-qr")]
                public IActionResult GenerateQRCode()
                {
                    try
                    {
                        var token = GenerateCustomToken();
                        var qrCodeImage = GenerateQRCodeImage(token);
                        return File(qrCodeImage, "image/png");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error generating QR code: {ex.Message}");
                        return StatusCode(500, new { Message = "Failed to generate QR code.", Error = ex.Message });
                    }
                }

            [HttpPost("check-in")]
                public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
                {
                    System.Diagnostics.Debug.WriteLine($"Received payload: {System.Text.Json.JsonSerializer.Serialize(request)}");

                    var authorization = Request.Headers["Authorization"].FirstOrDefault();
                    if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return Unauthorized(new { Success = false, Message = "Invalid or missing Authorization header." });

                    var accessToken = authorization.Substring("Bearer ".Length).Trim();

                    if (!_authService.ValidateAccessToken(accessToken))
                        return Unauthorized(new { Success = false, Message = "Invalid or expired access token." });

                    if (string.IsNullOrEmpty(request.UserName))
                        return BadRequest(new { Success = false, Message = "UserName is required." });

                    if (!ValidateCustomToken(request.Token))
                        return BadRequest(new { Success = false, Message = "Invalid or expired QR token." });

                    if (string.IsNullOrEmpty(request.MacAddress) || request.MacAddress == "00:00:00:00:00:00")
                        return BadRequest(new { Success = false, Message = "Invalid or missing device identifier." });

                    var jakartaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _jakartaTimeZone);
                    var checkInTimeUtc = DateTime.UtcNow;

                    var startOfDayJakarta = jakartaNow.Date;
                    var startOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(startOfDayJakarta, _jakartaTimeZone);
                    var endOfDayUtc = startOfDayUtc.AddDays(1).AddTicks(-1);

                    var setting = await _context.AttendanceSettings.OrderByDescending(s => s.UpdatedAt).FirstOrDefaultAsync();
                    if (setting == null)
                        return StatusCode(500, new { Message = "Attendance schedule is not configured." });

                    var jamMasuk = startOfDayJakarta.Add(setting.CheckInDeadline);
                    var jamIstirahat = startOfDayJakarta.Add(setting.BreakStartTime);
                    var jamKembali = startOfDayJakarta.Add(setting.BreakEndTime);
                    var jamPulang = startOfDayJakarta.Add(setting.CheckOutTime);
                    var kembaliTepatWaktuMulai = jamKembali.AddMinutes(-15);

                    var existing = await _context.Presensis.FirstOrDefaultAsync(p =>
                        p.UserName == request.UserName &&
                        p.CheckInTime >= startOfDayUtc &&
                        p.CheckInTime <= endOfDayUtc);

                    // === LOGIKA CHECK-IN PERTAMA ===
                    if (existing == null)
                    {
                        string status;
                        if (jakartaNow < jamMasuk)
                            status = "Tepat Waktu";
                        else if (jakartaNow >= jamMasuk && jakartaNow < jamIstirahat)
                            status = "Terlambat";
                        else
                            return BadRequest(new { Success = false, Message = "Sudah melewati waktu check-in. Tidak bisa presensi." });

                        var presensi = new Presensi
                        {
                            UserName = request.UserName,
                            IpAddress = request.MobileIpAddress,
                            MacAddress = request.MacAddress,
                            DeviceName = request.DeviceName,
                            CheckInTime = checkInTimeUtc,
                            Status = status,
                            Token = request.Token
                        };

                        _context.Presensis.Add(presensi);
                        await _context.SaveChangesAsync();

                        await _notificationService.SendCheckInNotificationAsync(new CheckInNotificationRequest
                        {
                            UserId = request.UserName,
                            UserName = request.UserName,
                            CheckInTime = jakartaNow
                        });

                        lock (_lock) { _activeTokens.Remove(request.Token); }

                        return Ok(new { Success = true, Message = $"Presensi berhasil: {status}", PresensiId = presensi.Id });
                    }

                    // === LOGIKA ISTIRAHAT ===
                    if (!existing.BreakStartTime.HasValue)
                    {
                        if (jakartaNow < jamIstirahat)
                            return BadRequest(new { Success = false, Message = "Belum waktunya istirahat." });

                        if (jakartaNow >= jamIstirahat && jakartaNow < jamKembali)
                        {
                            existing.BreakStartTime = checkInTimeUtc;
                            await _context.SaveChangesAsync();

                            lock (_lock) { _activeTokens.Remove(request.Token); }

                            return Ok(new { Success = true, Message = "Istirahat dimulai.", PresensiId = existing.Id });
                        }
                    }

                    // === LOGIKA KEMBALI ===
                    if (existing.BreakStartTime.HasValue && !existing.BreakEndTime.HasValue)
                    {
                        if (jakartaNow < kembaliTepatWaktuMulai)
                            return BadRequest(new { Success = false, Message = "Belum waktunya kembali dari istirahat." });

                        if (jakartaNow >= kembaliTepatWaktuMulai && jakartaNow <= jamKembali)
                        {
                            existing.BreakEndTime = checkInTimeUtc;
                            await _context.SaveChangesAsync();

                            lock (_lock) { _activeTokens.Remove(request.Token); }

                            return Ok(new { Success = true, Message = "Kembali tepat waktu.", PresensiId = existing.Id });
                        }

                        if (jakartaNow > jamKembali)
                        {
                            existing.BreakEndTime = checkInTimeUtc;
                            await _context.SaveChangesAsync();

                            lock (_lock) { _activeTokens.Remove(request.Token); }

                            return Ok(new { Success = true, Message = "Terlambat kembali.", PresensiId = existing.Id });
                        }
                    }

                    // === LOGIKA PULANG ===
                    if (!existing.CheckOutTime.HasValue)
                    {
                        if (jakartaNow < jamPulang)
                            return BadRequest(new { Success = false, Message = "Belum waktunya pulang." });

                        if (jakartaNow >= jamPulang)
                        {
                            existing.CheckOutTime = checkInTimeUtc;
                            await _context.SaveChangesAsync();

                            lock (_lock) { _activeTokens.Remove(request.Token); }

                            return Ok(new { Success = true, Message = "Pulang berhasil.", PresensiId = existing.Id });
                        }
                    }

                    return BadRequest(new { Success = false, Message = "Presensi tidak valid atau sudah dilakukan semua." });
                }

                [HttpGet("all")]
                public async Task<IActionResult> GetAllAttendance()
                {
                    var attendanceRecords = await _context.Presensis
                        .Select(p => new
                        {
                            p.Id,
                            p.UserName,
                            p.IpAddress,
                            p.MacAddress,
                            p.DeviceName,
                            p.Status,
                            CheckInTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)p.CheckInTime, _jakartaTimeZone),
                            BreakStartTime = p.BreakStartTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakStartTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            BreakEndTime = p.BreakEndTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakEndTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            CheckOutTime = p.CheckOutTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.CheckOutTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            p.Token
                        })
                        .ToListAsync();

                    return Ok(attendanceRecords);
                }

                [HttpGet("user/{userName}")]
                public async Task<IActionResult> GetAttendanceByUser(string userName)
                {
                    var authorization = Request.Headers["Authorization"].FirstOrDefault();
                    if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return Unauthorized(new { Success = false, Message = "Invalid or missing Authorization header." });

                    var accessToken = authorization.Substring("Bearer ".Length).Trim();

                    if (!_authService.ValidateAccessToken(accessToken))
                        return Unauthorized(new { Success = false, Message = "Invalid or expired access token." });

                    var attendanceRecords = await _context.Presensis
                        .Where(p => p.UserName == userName)
                        .Select(p => new
                        {
                            p.Id,
                            p.UserName,
                            p.IpAddress,
                            p.MacAddress,
                            p.DeviceName,
                            p.Status,
                            CheckInTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)p.CheckInTime, _jakartaTimeZone),
                            BreakStartTime = p.BreakStartTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakStartTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            BreakEndTime = p.BreakEndTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakEndTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            CheckOutTime = p.CheckOutTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.CheckOutTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            p.Token
                        })
                        .ToListAsync();

                    if (!attendanceRecords.Any())
                        return NotFound(new { Success = false, Message = $"No attendance records found for user with UserName {userName}." });

                    return Ok(attendanceRecords);
                }


                [HttpGet("{id}")]
                public async Task<IActionResult> GetAttendanceById(int id)
                {
                    var authorization = Request.Headers["Authorization"].FirstOrDefault();
                    if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return Unauthorized(new { Success = false, Message = "Invalid or missing Authorization header." });

                    var accessToken = authorization.Substring("Bearer ".Length).Trim();

                    if (!_authService.ValidateAccessToken(accessToken))
                        return Unauthorized(new { Success = false, Message = "Invalid or expired access token." });

                    var attendanceRecord = await _context.Presensis
                        .Where(p => p.Id == id)
                        .Select(p => new
                        {
                            p.Id,
                            p.UserName,
                            p.IpAddress,
                            p.MacAddress,
                            p.DeviceName,
                            p.Status,
                            CheckInTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)p.CheckInTime, _jakartaTimeZone),
                            BreakStartTime = p.BreakStartTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakStartTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            BreakEndTime = p.BreakEndTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.BreakEndTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            CheckOutTime = p.CheckOutTime.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(p.CheckOutTime.Value, _jakartaTimeZone) : (DateTime?)null,
                            p.Token
                        })
                        .FirstOrDefaultAsync();

                    if (attendanceRecord == null)
                        return NotFound(new { Success = false, Message = $"No attendance record found with ID {id}." });

                    return Ok(attendanceRecord);
                }

                private string GenerateCustomToken()
                {
                    var guid = Guid.NewGuid();
                    var bytes = guid.ToByteArray();
                    var hex = BitConverter.ToString(bytes).Replace("-", "");

                    if (hex.Length != 32)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: Hex string length is {hex.Length}, expected 32.");
                        throw new InvalidOperationException("Generated hex string length is invalid.");
                    }

                    var formattedToken = $"{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 4)}-{hex.Substring(24, 4)}-{hex.Substring(28, 4)}";
                    System.Diagnostics.Debug.WriteLine($"Generated token: {formattedToken}");

                    lock (_lock)
                    {
                        var expiryTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _jakartaTimeZone).AddSeconds(60);
                        _activeTokens[formattedToken] = expiryTime;
                    }

                    return formattedToken;
                }

                private static byte[] GenerateQRCodeImage(string token)
                {
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new PngByteQRCode(qrCodeData);
                    return qrCode.GetGraphic(20);
                }

                private bool ValidateCustomToken(string token)
                {
                    lock (_lock)
                    {
                        if (!_activeTokens.TryGetValue(token, out var expiryTime))
                            return false;

                        var jakartaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _jakartaTimeZone);
                        if (jakartaNow > expiryTime)
                        {
                            _activeTokens.Remove(token);
                            return false;
                        }

                        return true;
                    }
                }
            }
        }