using PresensiQRBackend.Models;
using System.Threading.Tasks;

namespace PresensiQRBackend.Services
{
    public interface INotificationService
    {
        // Task RegisterTokenAsync(string userId, string fcmToken);
        Task SendCheckInNotificationAsync(CheckInNotificationRequest request);
        Task SendPermissionNotificationAsync(PermissionNotificationRequest request);
        Task SendPermissionStatusNotificationAsync(PermissionStatusNotificationRequest request);
    }
}