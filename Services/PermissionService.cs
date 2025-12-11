using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;

namespace PresensiQRBackend.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;

        public PermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<object> SubmitIzinAsync(IzinCreateRequest request)
        {
             var izin = new Izin
    {
        UserId = request.UserId,
        Nama = request.Nama,
        JenisIzin = request.JenisIzin,
        TanggalMulai = request.TanggalMulai,
        TanggalSelesai = request.TanggalSelesai,
        Alasan = request.Alasan,
        Status = request.Status,
        FotoBase64 = request.FotoBase64,
        TanggalPengajuan = request.TanggalPengajuan,
        PembimbingChatId = request.PembimbingChatId,
        KodeIzin = request.KodeIzin
    };
            _context.Izins.Add(izin);
            await _context.SaveChangesAsync();
            return new { IzinId = izin.Id, Success = true, Message = "Izin submitted." };
        }
    }
}