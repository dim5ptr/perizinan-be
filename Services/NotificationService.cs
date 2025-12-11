using Microsoft.Extensions.Configuration;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore; // Untuk FirstOrDefaultAsync dan ToListAsync
using System.Net.Http.Headers; // Untuk AuthenticationHeaderValue

namespace PresensiQRBackend.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string? _serviceAccountFilePath;
        private readonly string? _projectId;
        private readonly string? _telegramBotToken;
        private readonly string[]? _telegramChatIds;

        public NotificationService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient();
            _serviceAccountFilePath = configuration["FcmConfig:ServiceAccountFilePath"];
            _projectId = configuration["FcmConfig:ProjectId"];
            _telegramBotToken = configuration["TelegramBotToken"];
            _telegramChatIds = configuration.GetSection("TelegramChatIds").Get<string[]>();
        }

        public async Task SendCheckInNotificationAsync(CheckInNotificationRequest request)
        {
            var userToken = await _context.FcmTokens.FirstOrDefaultAsync(t => t.UserId == request.UserId);
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
        }

        public async Task SendPermissionNotificationAsync(PermissionNotificationRequest request)
        {
            // Validasi ChatId
            if (string.IsNullOrEmpty(request.ChatId))
            {
                throw new ArgumentException("ChatId diperlukan.");
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
                        data = new { izinId = request.IzinId.ToString() }
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
            await SendTelegramNotificationAsync(telegramMessage, request.ChatId);
        }

        public async Task SendPermissionStatusNotificationAsync(PermissionStatusNotificationRequest request)
        {
            var userToken = await _context.FcmTokens.FirstOrDefaultAsync(t => t.UserId == request.UserId);
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
                        data = new { izinId = request.IzinId.ToString() }
                    }
                };
                await SendFcmNotification(payload);
            }
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
                throw new Exception($"Failed to send FCM notification: {error}");
            }
        }

        private async Task<(bool Success, string Message)> SendTelegramNotificationAsync(string message, string chatId)
        {
            try
            {
                var telegramUrl = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage";
                var body = new
                {
                    chat_id = chatId,
                    text = message
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(body),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json"));

                var response = await _httpClient.PostAsync(telegramUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Gagal mengirim notifikasi Telegram: HTTP {response.StatusCode}");
                }

                var telegramResponse = JsonConvert.DeserializeObject<TelegramResponse>(responseContent); // Perbaikan deserialisasi
                if (telegramResponse != null && !telegramResponse.Ok)
                {
                    return (false, $"Error Telegram: {telegramResponse.Description}");
                }

                return (true, "Notifikasi Telegram berhasil dikirim");
            }
            catch (Exception ex)
            {
                return (false, $"Error mengirim notifikasi Telegram: {ex.Message}");
            }
        }

        class TelegramResponse
        {
            public bool Ok { get; set; }
            public int? ErrorCode { get; set; }
            public string? Description { get; set; }
        }
    }
}
