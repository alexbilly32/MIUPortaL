

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentPhotoController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<StudentPhotoController> _logger;

        public StudentPhotoController(MIUContext context, ILogger<StudentPhotoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("upload-photo/{regNumber}")]
        public async Task<IActionResult> UploadStudentPhoto(string regNumber, IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return BadRequest("No photo file provided.");

            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest("Only JPG and PNG files are allowed.");

            if (photo.Length > 2 * 1024 * 1024) // 2MB cap
                return BadRequest("Photo must be under 2MB.");

            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null)
                return NotFound("Student not found.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "student-photos");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string safeRegNumber = regNumber.Replace("/", "_");
            string fileName = $"{safeRegNumber}{ext}";
            string absoluteFilePath = Path.Combine(uploadsFolder, fileName);

            
            using (var stream = new FileStream(absoluteFilePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

           
            string relativePath = $"/uploads/student-photos/{fileName}";
            student.PhotoPath = relativePath;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Photo uploaded for student {regNumber}: {relativePath}");

            return Ok(new { message = "Photo uploaded successfully.", photoPath = relativePath });
        }

        [HttpGet("photo-status/{regNumber}")]
        public async Task<IActionResult> GetPhotoStatus(string regNumber)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
            if (student == null)
                return NotFound("Student not found.");

            return Ok(new
            {
                hasPhoto = !string.IsNullOrEmpty(student.PhotoPath),
                photoPath = student.PhotoPath
            });
        }
    }
}