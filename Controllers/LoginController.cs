using Microsoft.AspNetCore.Mvc;
using PresensiQRBackend.Models;
using PresensiQRBackend.Services;
using Microsoft.Extensions.Logging;
using PresensiQRBackend.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace PresensiQRBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AuthService authService, ApplicationDbContext context, ILogger<LoginController> logger)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
        }

        
  [HttpPost]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
    {
        _logger.LogWarning("Login request is invalid: Email or password is missing.");
        return BadRequest(new { Message = "Email and password are required." });
    }

    _logger.LogInformation("Attempting login for email: {Email}", request.Email);
    var loginResponse = await _authService.LoginAsync(request.Email, request.Password);

    if (loginResponse == null)
    {
        _logger.LogError("LoginResponse is null. API might be unreachable.");
        return StatusCode(503, new { Message = "Unable to connect to authentication server." });
    }

    if (!loginResponse.Success)
    {
        _logger.LogWarning("Login failed for email: {Email}. Message: {Message}",
            request.Email, loginResponse.Message);
        return Unauthorized(new { Message = loginResponse.Message ?? "Invalid email or password." });
    }

    if (loginResponse.Data == null || string.IsNullOrEmpty(loginResponse.Data.AccessToken))
    {
        _logger.LogWarning("Login successful but no access token was provided for email: {Email}.", request.Email);
        return BadRequest(new { Message = "Login successful, but no access token was provided by the authentication server." });
    }

    _logger.LogDebug("LoginController: loginResponse.Data.AccessToken = {AccessToken}", loginResponse.Data.AccessToken);

    try
    {
        var email = loginResponse.Data.Email ?? request.Email;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                Username = email.Split('@')[0],
                Nama = loginResponse.Data.PersonalInfo?.FullName ?? email,
                AsalSekolah = "-",
                Email = email,
                Role = "1",
                NomorTelepon = loginResponse.Data.PersonalInfo?.Phone ?? "-",
                TanggalLahir = DateTime.TryParse(loginResponse.Data.PersonalInfo?.Birthday, out var birthday) ? birthday : default,
                Gender = loginResponse.Data.PersonalInfo?.Gender switch
                {
                    1 => "Male",
                    2 => "Female",
                    _ => "-"
                },
                FotoProfil = loginResponse.Data.PersonalInfo?.ProfilePicture ?? "",
                FcmToken = ""
            };


            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Gagal menyimpan user baru. Detail: {Error}", dbEx.InnerException?.Message);
                return StatusCode(500, "Error saat menyimpan data user: " + dbEx.InnerException?.Message);
            }

            var password = new Password
            {
                UserId = user.Id,
                PasswordHash = HashPassword(request.Password)
            };

            _context.Passwords.Add(password);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User baru disimpan: Email={Email}, UserId={UserId}", email, user.Id);
        }
        else
        {
            _logger.LogInformation("User existing ditemukan: Email={Email}, UserId={UserId}", user.Email, user.Id);
        }

        if (loginResponse.UserId == null)
        {
            loginResponse.UserId = user.Id;
        }

        return Ok(new
        {
            AccessToken = loginResponse.Data.AccessToken,
            Email = user.Email,
            RoleId = user.Role,
            UserId = user.Id,
            Message = loginResponse.Message,
            Timestamp = loginResponse.Timestamp,
            Result = loginResponse.Result,
            PersonalInfo = loginResponse.Data.PersonalInfo
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error saat menyimpan atau mengupdate user {Email}: {Message}", request.Email, ex.Message);
        return StatusCode(500, "Error saat menyimpan data user: " + ex.Message);
    }
}



        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                _logger.LogWarning("Logout request failed: Authorization header is missing or invalid.");
                return Unauthorized(new { Message = "Authorization token is required." });
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            _logger.LogInformation("Attempting logout for token: {AccessToken}", accessToken.Substring(0, Math.Min(accessToken.Length, 10)));

            var logoutResponse = await _authService.LogoutAsync(accessToken);
            if (logoutResponse == null)
            {
                _logger.LogError("Logout failed: Unable to connect to authentication server.");
                return StatusCode(503, new { Message = "Unable to connect to authentication server." });
            }

            if (!logoutResponse.Success)
            {
                _logger.LogWarning("Logout failed for token: {AccessToken}. Message: {Message}",
                    accessToken.Substring(0, Math.Min(accessToken.Length, 10)), logoutResponse.Message);
                return Ok(new { Message = logoutResponse.Message ?? "Logout failed on server, but token cleared locally." });
            }

            _logger.LogInformation("Logout successful for token: {AccessToken}",
                accessToken.Substring(0, Math.Min(accessToken.Length, 10)));
            return Ok(new { Message = logoutResponse.Message ?? "Logout successful." });
        }

        // Fungsi dummy untuk hashing password
        private string HashPassword(string password)
        {
            // Ganti dengan BCrypt.Net.BCrypt.HashPassword(password) kalau pakai BCrypt
            return password; // Sementara
        }
    }
}
