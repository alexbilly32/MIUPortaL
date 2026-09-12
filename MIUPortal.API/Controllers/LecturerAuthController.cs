using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class LecturerAuthController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<LecturerAuthController> _logger;
        private readonly JwtService _jwtService;

        public LecturerAuthController(
            MIUContext context,
            ILogger<LecturerAuthController> logger,
            JwtService jwtService)
        {
            _context = context;
            _logger = logger;
            _jwtService = jwtService;
        }

      
        [HttpPost("lecturer-login")]
        public async Task<IActionResult> LecturerLogin([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email and password are required"
                    });
                }

                var lecturer = await _context.Lecturers
                    .Include(l => l.Faculty)
                    .FirstOrDefaultAsync(l => l.Email == request.Email);

                if (lecturer == null || lecturer.Status != "Active")
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid credentials or inactive account"
                    });
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, lecturer.PasswordHash))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Invalid credentials"
                    });
                }

                var token = _jwtService.GenerateLecturerJwt(lecturer);

                return Ok(new
                {
                    success = true,
                    message = "Login successful",

                    token,

                    mustChangePassword = lecturer.MustChangePassword,

                    lecturer = new
                    {
                        lecturerId = lecturer.LecturerId,
                        email = lecturer.Email,
                        firstName = lecturer.FirstName,
                        lastName = lecturer.LastName,
                        fullName = $"{lecturer.FirstName} {lecturer.LastName}",
                        facultyId = lecturer.FacultyId,
                        facultyName = lecturer.Faculty?.FacultyName ?? "N/A",
                        phoneNumber = lecturer.PhoneNumber,
                        status = lecturer.Status,
                       
                        isDean = lecturer.IsDean
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lecturer login error");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        // ========== CHANGE PASSWORD AFTER FIRST LOGIN ==========
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (request == null ||
                   string.IsNullOrWhiteSpace(request.Email) ||
                   string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Email and new password are required"
                    });
                }

                var lecturer = await _context.Lecturers
                    .FirstOrDefaultAsync(l => l.Email == request.Email);

                if (lecturer == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Lecturer account not found"
                    });
                }

                // PASSWORD POLICY
                bool strongPassword =
                    request.NewPassword.Length >= 8 &&
                    request.NewPassword.Any(char.IsUpper) &&
                    request.NewPassword.Any(char.IsLower) &&
                    request.NewPassword.Any(char.IsDigit) &&
                    request.NewPassword.Any(ch => !char.IsLetterOrDigit(ch));

                if (!strongPassword)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Password must contain uppercase, lowercase, number, special character and minimum 8 characters"
                    });
                }

                lecturer.PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                lecturer.MustChangePassword = false;
                lecturer.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Password changed successfully"
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password change failed");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Server error"
                });
            }
        }
    }

    // DTO
    public class LoginRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string? Email { get; set; }

        public string? NewPassword { get; set; }
    }
}