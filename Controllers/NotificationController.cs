using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth.OAuth2;
using System.Net.Http.Headers;

namespace PresensiQRBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string? _serviceAccountFilePath;
        private readonly string? _projectId;
        private readonly string? _telegramBotToken;
        private readonly string[]? _telegramChatIds;

        public NotificationController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
            _serviceAccountFilePath = configuration["FcmConfig:ServiceAccountFilePath"];
            _projectId = configuration["FcmConfig:ProjectId"];
            _telegramBotToken = configuration["TelegramBotToken"];
            _telegramChatIds = configuration.GetSection("TelegramChatIds").Get<string[]>();
        }

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] TokenRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.FcmToken))
                return BadRequest(new { Success = false, Message = "UserId and FcmToken are required." });

            var existingToken = await _context.FcmTokens
                .FirstOrDefaultAsync(t => t.UserId == request.UserId);
            if (existingToken == null)
            {
                _context.FcmTokens.Add(new FcmToken
                {
                    UserId = request.UserId,
                    Token = request.FcmToken,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingToken.Token = request.FcmToken;
                existingToken.CreatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "FCM token registered successfully." });
        }

        [HttpPost("send-checkin-notification")]
        public async Task<IActionResult> SendCheckInNotification([FromBody] CheckInNotificationRequest request)
        {
            var userToken = await _context.FcmTokens
                .FirstOrDefaultAsync(t => t.UserId == request.UserId);
            if (userToken != null)
            {
                var payload = new
                {
                    message = new
                    {
                        token = userToken.Token,
                        notification = new
                        {
                            title = "Check-In Berhasil",
                            body = $"Anda telah check-in pada {request.CheckInTime:dd MMMM yyyy HH:mm} WIB."
                        }
                    }
                };
                await SendFcmNotification(payload);
            }

            var jakartaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta"));
            var startOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(jakartaNow.Date, TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta"));
            var endOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(jakartaNow.Date.AddDays(1).AddTicks(-1), TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta"));

            var otherUsers = await _context.Presensis
                .Where(p => p.UserName != request.UserName && p.CheckInTime >= startOfDayUtc && p.CheckInTime <= endOfDayUtc)
                .Select(p => p.UserName)
                .Distinct()
                .ToListAsync();

            foreach (var userName in otherUsers)
            {
                var token = await _context.FcmTokens
                    .FirstOrDefaultAsync(t => t.UserId == userName);
                if (token != null)
                {
                    var payload = new
                    {
                        message = new
                        {
                            token = token.Token,
                            notification = new
                            {
                                title = "Check-In Baru",
                                body = $"{request.UserName} telah check-in pagi ini."
                            }
                        }
                    };
                    await SendFcmNotification(payload);
                }
            }

            return Ok(new { Success = true, Message = "Check-in notifications sent." });
        }

        [HttpPost("send-permission-notification")]
        public async Task<IActionResult> SendPermissionNotification([FromBody] PermissionNotificationRequest request)
        {
            // Validasi ChatId
            if (string.IsNullOrEmpty(request.ChatId))
            {
                return BadRequest(new { Success = false, Message = "ChatId diperlukan." });
            }

            // Kirim notifikasi FCM ke admin
            var adminTokens = await _context.FcmTokens
                .Where(t => t.UserId.StartsWith("admin_"))
                .Select(t => t.Token)
                .ToListAsync();

            foreach (var token in adminTokens)
            {
                var payload = new
                {
                    message = new
                    {
                        token = token,
                        notification = new
                        {
                            title = "Pengajuan Izin Baru",
                            body = $"{request.UserName} mengajukan izin {request.JenisIzin} mulai {request.TanggalMulai:dd MMMM yyyy}."
                        },
                        data = new
                        {
                            izinId = request.IzinId.ToString()
                        }
                    }
                };
                await SendFcmNotification(payload);
            }

            // Siapkan pesan Telegram
            var telegramMessage = $"Pengajuan izin baru dari {request.UserName}\n" +
                                $"Jenis Izin: {request.JenisIzin}\n" +
                                $"Tanggal Mulai: {request.TanggalMulai:dd MMMM yyyy}\n" +
                                $"Alasan: {request.Alasan}\n" +
                                $"Silakan tinjau di website.";

            // Kirim notifikasi Telegram menggunakan ChatId dari request
            await SendTelegramNotification(telegramMessage, request.ChatId);

            return Ok(new { Success = true, Message = "Notifikasi izin berhasil dikirim." });
        }

        [HttpPost("telegram-configs")]
public async Task<IActionResult> AddTelegramConfig([FromBody] CreateTelegramConfigRequest request)
{
    var config = new TelegramConfig
    {
        ChatId = request.ChatId,
        TopicId = request.TopicId,
        Description = request.Description,
        IsAktif = true // default aktif
    };

    _context.TelegramConfigs.Add(config);
    await _context.SaveChangesAsync();

    return Ok(new { Success = true, Message = "Config berhasil ditambahkan." });
}

[HttpGet("telegram-configs/aktif")]
public async Task<IActionResult> GetAktifTelegramConfigs()
{
    var aktifConfigs = await _context.TelegramConfigs
        .Where(c => c.IsAktif)
        .ToListAsync();

    return Ok(new
    {
        Success = true,
        Data = aktifConfigs
    });
}

[HttpPut("telegram-configs/status")]
public async Task<IActionResult> UpdateTelegramConfigStatus([FromBody] UpdateTelegramConfigStatusRequest request)
{
    var config = await _context.TelegramConfigs.FindAsync(request.Id);
    if (config == null)
    {
        return NotFound(new { Success = false, Message = "Data tidak ditemukan." });
    }

    config.IsAktif = request.IsAktif;
    await _context.SaveChangesAsync();

    return Ok(new { Success = true, Message = "Status berhasil diperbarui." });
}


[HttpPost("telegram-presensi")]
public async Task<IActionResult> SendTelegramPresensi([FromBody] TelegramPresensiRequest request)
{
    var message = $@"🔔 NOTIFIKASI PRESENSI {request.PresensiType.ToUpper()}

📄 PRESENSI {request.PresensiType} ({request.WaktuPresensi} WIB)
👤 Nama       : {request.UserName}
📅 Hari       : {request.DayName}
📆 Tanggal    : {request.FormattedDate}
⏰ Waktu Scan : {request.ScanTimeWIB}
📌 Status     : {request.Status.ToUpper()}
📱 Perangkat  : {request.DeviceName} 
🌐 IP Address : {request.IpAddress}
———————————————

Terima kasih telah melakukan presensi! 🙏";

    try
    {
        var telegramConfigs = await _context.TelegramConfigs
            .Where(c => c.IsAktif)
            .ToListAsync();

        // ✅ Deklarasi list hasil pengiriman
        var hasilPengiriman = new List<object>();

        // ✅ Kirim ke semua grup yang aktif
        foreach (var config in telegramConfigs)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.TopicId))
                {
                    await SendTelegramNotificationToTopic(message, config.ChatId, config.TopicId);
                }
                else
                {
                    await SendTelegramNotification(message, config.ChatId);
                }

                hasilPengiriman.Add(new
                {
                    ChatId = config.ChatId,
                    TopicId = config.TopicId,
                    Status = "✅ BERHASIL"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error sending to topic: {ex.Message}");

                hasilPengiriman.Add(new
                {
                    ChatId = config.ChatId,
                    TopicId = config.TopicId,
                    Status = "❌ GAGAL",
                    Error = ex.Message
                });
            }
        }

        return Ok(new
        {
            Success = true,
            Message = $"Notifikasi selesai dikirim ke {hasilPengiriman.Count} grup aktif.",
            Result = hasilPengiriman
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            Success = false,
            Message = $"Gagal kirim notifikasi: {ex.Message}"
        });
    }
}




        [HttpPost("send-permission-status-notification")]
        public async Task<IActionResult> SendPermissionStatusNotification([FromBody] PermissionStatusNotificationRequest request)
        {
            var userToken = await _context.FcmTokens
                .FirstOrDefaultAsync(t => t.UserId == request.UserId);
            if (userToken != null)
            {
                var payload = new
                {
                    message = new
                    {
                        token = userToken.Token,
                        notification = new
                        {
                            title = "Status Pengajuan Izin",
                            body = $"Pengajuan izin Anda telah {(request.IsApproved ? "diterima" : "ditolak")}."
                        },
                        data = new
                        {
                            izinId = request.IzinId.ToString()
                        }
                    }
                };
                await SendFcmNotification(payload);
            }

            return Ok(new { Success = true, Message = "Permission status notification sent." });
        }

        private async Task SendFcmNotification(object payload)
        {
            if (string.IsNullOrEmpty(_serviceAccountFilePath) || string.IsNullOrEmpty(_projectId))
            {
                throw new Exception("FCM configuration is missing or invalid.");
            }

            var credential = GoogleCredential.FromFile(_serviceAccountFilePath)
                .CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" });

            var serviceAccountCredential = credential.UnderlyingCredential as ServiceAccountCredential;
            if (serviceAccountCredential == null)
            {
                throw new Exception("Invalid Service Account credential.");
            }

            var accessToken = await serviceAccountCredential.GetAccessTokenForRequestAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception("Failed to retrieve access token.");
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PostAsync(
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"FCM Error: {error}");
                throw new Exception($"Failed to send FCM notification: {error}");
            }
            System.Diagnostics.Debug.WriteLine("FCM Notification sent successfully.");
        }

        private async Task SendTelegramNotification(string message, string chatId)
        {
            var url = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Telegram Error: {error}");
                throw new Exception($"Failed to send Telegram notification: {error}");
            }
            System.Diagnostics.Debug.WriteLine($"Telegram Notification sent to Chat ID: {chatId}");
        }

        private async Task SendTelegramNotificationToTopic(string message, string chatId, string topicId)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage?chat_id={chatId}&message_thread_id={topicId}&text={Uri.EscapeDataString(message)}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Telegram Topic Error: {error}");
                    
                    // Jika gagal dengan topic, coba kirim tanpa topic sebagai fallback
                    System.Diagnostics.Debug.WriteLine($"Mencoba kirim tanpa topic sebagai fallback...");
                    await SendTelegramNotification(message, chatId);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Telegram Notification sent to Chat ID: {chatId}, Topic ID: {topicId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error sending to topic, trying without topic: {ex.Message}");
                // Fallback ke pengiriman normal tanpa topic
                await SendTelegramNotification(message, chatId);
            }
        }

        // Method tambahan untuk testing konfigurasi Telegram
        [HttpGet("test-telegram-config")]
        public IActionResult TestTelegramConfig()
        {
            return Ok(new
            {
                BotTokenConfigured = !string.IsNullOrEmpty(_telegramBotToken),
                ChatIdsCount = _telegramChatIds?.Length ?? 0,
                ChatIds = _telegramChatIds ?? new string[0]
            });
        }
    }
}