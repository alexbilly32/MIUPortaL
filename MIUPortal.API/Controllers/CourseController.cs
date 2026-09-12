using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourseController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<CourseController> _logger;

        public CourseController(MIUContext context, ILogger<CourseController> logger)
        {
            _context = context;
            _logger = logger;
        }

       
        [HttpGet]
        public async Task<IActionResult> GetAllCourses()
        {
            try
            {
                var courses = await _context.Courses
                    .Where(c => (c.IsActive ?? true))
                    .OrderBy(c => c.ProgrammeCode)
                    .ThenBy(c => c.Semester)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = courses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllCourses");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

      
        [HttpGet("programme/{programmeCode}")]
        public async Task<IActionResult> GetCoursesByProgramme(string programmeCode)
        {
            try
            {
                var courses = await _context.Courses
                    .Where(c => c.ProgrammeCode == programmeCode && (c.IsActive ?? true))
                    .OrderBy(c => c.Semester)
                    .ToListAsync();

                if (!courses.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No courses found",
                        data = new List<object>()
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = courses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCoursesByProgramme");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

       
        [HttpGet("{courseId}")]
        public async Task<IActionResult> GetCourse(int courseId)
        {
            try
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.CourseId == courseId);

                if (course == null)
                {
                    return NotFound(new { message = "Course not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = course
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCourse");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }
    }
}
