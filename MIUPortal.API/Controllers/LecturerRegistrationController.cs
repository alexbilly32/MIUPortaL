using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/lecturer")]
    [Authorize]
    public class LecturerRegistrationController(MIUContext context) : ControllerBase
    {
        private readonly MIUContext _context = context;

        // ========== CREATE LECTURER (ADMIN ONLY) ==========
        [HttpPost("register")]
        public async Task<IActionResult> RegisterLecturer([FromBody] CreateLecturerRequest request)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.FirstName)
                    || string.IsNullOrWhiteSpace(request?.LastName) || string.IsNullOrWhiteSpace(request?.Password))
                    return BadRequest(new { success = false, message = "All fields are required" });

                // Check if email already exists
                var existingLecturer = await _context.Lecturers
                    .FirstOrDefaultAsync(l => l.Email == request.Email);

                if (existingLecturer != null)
                    return BadRequest(new { success = false, message = "Email already registered" });

                // Validate faculty exists
                if (request.FacultyId.HasValue)
                {
                    var faculty = await _context.Faculties
                        .FirstOrDefaultAsync(f => f.FacultyId == request.FacultyId);

                    if (faculty == null)
                        return BadRequest(new { success = false, message = "Invalid faculty" });
                }

                // Hash password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Create lecturer
                var lecturer = new Lecturer
                {
                    Email = request.Email,
                    PasswordHash = passwordHash,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    FacultyId = request.FacultyId,
                    Status = "Active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Lecturers.Add(lecturer);
                await _context.SaveChangesAsync();

                // Assign courses if provided
                if (request.CourseIds != null && request.CourseIds.Count > 0)
                {
                    foreach (var courseId in request.CourseIds)
                    {
                        var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
                        if (course != null)
                        {
                            var lecturerCourse = new LecturerCourse
                            {
                                LecturerId = lecturer.LecturerId,
                                CourseId = courseId,
                                AcademicYear = request.AcademicYear ?? DateTime.Now.Year,
                                Semester = request.Semester ?? 1,
                                CreatedAt = DateTime.Now
                            };

                            _context.LecturerCourses.Add(lecturerCourse);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = "Lecturer registered successfully",
                    lecturer = new
                    {
                        lecturerId = lecturer.LecturerId,
                        email = lecturer.Email,
                        firstName = lecturer.FirstName,
                        lastName = lecturer.LastName,
                        facultyId = lecturer.FacultyId,
                        status = lecturer.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== GET ALL LECTURERS ==========
        [HttpGet("all")]
        public async Task<IActionResult> GetAllLecturers()
        {
            try
            {
                var lecturers = await _context.Lecturers
                    .Include(l => l.Faculty)
                    .OrderBy(l => l.FirstName)
                    .ToListAsync();

                var result = lecturers.Select(l => new
                {
                    lecturerId = l.LecturerId,
                    email = l.Email,
                    firstName = l.FirstName,
                    lastName = l.LastName,
                    fullName = $"{l.FirstName} {l.LastName}",
                    faculty = l.Faculty != null ? new { id = l.Faculty.FacultyId, name = l.Faculty.FacultyName } : null,
                    status = l.Status,
                    createdAt = l.CreatedAt
                }).ToList();

                return Ok(new { success = true, data = result, count = result.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== GET LECTURER BY ID ==========
        [HttpGet("{lecturerId}")]
        public async Task<IActionResult> GetLecturerById(int lecturerId)
        {
            try
            {
                var lecturer = await _context.Lecturers
                    .Include(l => l.Faculty)
                    .FirstOrDefaultAsync(l => l.LecturerId == lecturerId);

                if (lecturer == null)
                    return NotFound(new { success = false, message = "Lecturer not found" });

                // Get courses taught by lecturer
                var courses = await _context.LecturerCourses
                    .Where(lc => lc.LecturerId == lecturerId)
                    .Include(lc => lc.Course)
                    .ToListAsync();

                var courseList = courses.Select(lc => new
                {
                    id = lc.Course!.CourseId,
                    code = lc.Course.CourseCode,
                    name = lc.Course.CourseName,
                    academicYear = lc.AcademicYear,
                    semester = lc.Semester
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        lecturerId = lecturer.LecturerId,
                        email = lecturer.Email,
                        firstName = lecturer.FirstName,
                        lastName = lecturer.LastName,
                        phoneNumber = lecturer.PhoneNumber,
                        faculty = lecturer.Faculty != null ? new { id = lecturer.Faculty.FacultyId, name = lecturer.Faculty.FacultyName } : null,
                        courses = courseList,
                        status = lecturer.Status
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== ASSIGN COURSE TO LECTURER ==========
        [HttpPost("{lecturerId}/assign-course")]
        public async Task<IActionResult> AssignCourseToLecturer(int lecturerId, [FromBody] AssignCourseRequest request)
        {
            try
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == lecturerId);
                if (lecturer == null)
                    return NotFound(new { success = false, message = "Lecturer not found" });

                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == request.CourseId);
                if (course == null)
                    return NotFound(new { success = false, message = "Course not found" });

                // Check if already assigned
                var existing = await _context.LecturerCourses
                    .FirstOrDefaultAsync(lc => lc.LecturerId == lecturerId && lc.CourseId == request.CourseId
                        && lc.AcademicYear == request.AcademicYear && lc.Semester == request.Semester);

                if (existing != null)
                    return BadRequest(new { success = false, message = "Course already assigned to lecturer" });

                var lecturerCourse = new LecturerCourse
                {
                    LecturerId = lecturerId,
                    CourseId = request.CourseId,
                    AcademicYear = request.AcademicYear,
                    Semester = request.Semester,
                    CreatedAt = DateTime.Now
                };

                _context.LecturerCourses.Add(lecturerCourse);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Course assigned successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }

    public class CreateLecturerRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public int? FacultyId { get; set; }
        public List<int>? CourseIds { get; set; }
        public int? AcademicYear { get; set; }
        public int? Semester { get; set; }
    }


}