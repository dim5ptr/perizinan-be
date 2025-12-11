using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Models;

namespace PresensiQRBackend.Services
{
    public interface IPermissionService
    {
        Task<object> SubmitIzinAsync(IzinCreateRequest request);
    }
}