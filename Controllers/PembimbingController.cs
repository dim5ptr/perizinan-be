using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PresensiQRBackend.Data;
using PresensiQRBackend.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class PembimbingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PembimbingController> _logger;

    public PembimbingController(ApplicationDbContext context, ILogger<PembimbingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ✅ 1. Tambah Pembimbing
    [HttpPost]
    public async Task<IActionResult> CreatePembimbing([FromBody] CreatePembimbingDto dto)
    {
        try
        {
            var pembimbing = new Pembimbing
            {
                NamaPembimbing = dto.Nama,
                ChatId = dto.ChatId
            };

            _context.Pembimbing.Add(pembimbing);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Pembimbing berhasil ditambahkan.", Pembimbing = pembimbing });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal menambahkan pembimbing.");
            return StatusCode(500, "Terjadi kesalahan pada server.");
        }
    }

    // ✅ 2. Ambil Semua Pembimbing
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Pembimbing>>> GetAllPembimbing()
    {
        try
        {
            var pembimbings = await _context.Pembimbing.ToListAsync();
            return Ok(pembimbings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal mengambil daftar pembimbing.");
            return StatusCode(500, "Terjadi kesalahan pada server.");
        }
    }

    // ✅ 3. Ambil Pembimbing Berdasarkan ID
    [HttpGet("{id}")]
    public async Task<ActionResult<Pembimbing>> GetPembimbingById(int id)
    {
        try
        {
            var pembimbing = await _context.Pembimbing.FindAsync(id);
            if (pembimbing == null)
                return NotFound("Pembimbing tidak ditemukan.");

            return Ok(pembimbing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal mengambil pembimbing ID {Id}", id);
            return StatusCode(500, "Terjadi kesalahan pada server.");
        }
    }

    // ✅ 4. Update ChatId Pembimbing
    [HttpPut("{id}/chatid")]
    public async Task<IActionResult> UpdateChatId(int id, [FromBody] UpdateChatIdDto dto)
    {
        try
        {
            var pembimbing = await _context.Pembimbing.FindAsync(id);
            if (pembimbing == null)
                return NotFound("Pembimbing tidak ditemukan.");

            if (string.IsNullOrWhiteSpace(dto.ChatId))
                return BadRequest("ChatId tidak boleh kosong.");

            pembimbing.ChatId = dto.ChatId;
            await _context.SaveChangesAsync();
            return Ok(new { Message = "ChatId berhasil diupdate.", ChatId = pembimbing.ChatId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error update ChatId pembimbing ID {Id}", id);
            return StatusCode(500, "Terjadi kesalahan pada server.");
        }
    }

    // ✅ 5. Get ChatId Only (khusus)
    [HttpGet("{id}/chatid")]
    public async Task<IActionResult> GetChatId(int id)
    {
        try
        {
            var pembimbing = await _context.Pembimbing.FindAsync(id);
            if (pembimbing == null)
                return NotFound("Pembimbing tidak ditemukan.");

            return Ok(new { ChatId = pembimbing.ChatId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ambil ChatId pembimbing ID {Id}", id);
            return StatusCode(500, "Terjadi kesalahan pada server.");
        }
    }
}

// DTO Tambahan
public class CreatePembimbingDto
{
    public string Nama { get; set; } = null!;
    public string? ChatId { get; set; }
}

public class UpdateChatIdDto
{
    public string ChatId { get; set; } = null!;
}
