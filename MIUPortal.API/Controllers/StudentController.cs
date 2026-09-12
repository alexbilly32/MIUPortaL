using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Services;
using MIUPortal.API.Utilities;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly SemesterRegistrationCardService _cardService;
        private readonly ExaminationPermitService _permitService;
        private readonly IDocumentEmailService _emailService;
        private readonly ILogger<StudentController> _logger;


        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

        private readonly FeeScopingService _feeScopingService;

        public StudentController(
            MIUContext context,
            SemesterRegistrationCardService cardService,
            ExaminationPermitService permitService,
            IDocumentEmailService emailService,
            FeeScopingService feeScopingService,
            ILogger<StudentController> logger)
        {
            _context = context;
            _cardService = cardService;
            _permitService = permitService;
            _emailService = emailService;
            _feeScopingService = feeScopingService;
            _logger = logger;
        }

        
        private async Task<int?> GetCurrentGlobalSemesterIdAsync(string regNumber)
        {
            var current = await _context.StudentSemesterEnrollments
                .Where(e => e.RegNumber == regNumber && e.IsCurrent)
                .Select(e => (int?)e.SemesterId)
                .FirstOrDefaultAsync();

            return current;
        }

        [HttpGet("fees/{regNumber}")]
        public async Task<IActionResult> GetStudentFees(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);
                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;
                decimal totalRequired = semesterTuition + functionalFeesPerYear;

                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                decimal tuitionPaid = scoped.TuitionPaidThisSemester;
                decimal functionalPaid = scoped.FunctionalPaidThisYear;

                decimal totalPaid = tuitionPaid + functionalPaid;
                decimal tuitionShortfall = Math.Max(0, semesterTuition - tuitionPaid);
                decimal functionalShortfall = Math.Max(0, functionalFeesPerYear - functionalPaid);
                decimal totalShortfall = tuitionShortfall + functionalShortfall;

                decimal minimumTuitionRequired = semesterTuition * 0.5m;
                decimal minimumFunctionalRequired = functionalFeesPerYear * 0.5m;
                decimal minimumTotalRequired = minimumTuitionRequired + minimumFunctionalRequired;

                bool canEnroll = (tuitionPaid >= minimumTuitionRequired) && (functionalPaid >= minimumFunctionalRequired);
                bool has100PercentPaid = totalPaid >= totalRequired;

                var payments = await _context.Payments
                    .Where(p => p.RegNumber == regNumber && p.Status == "Approved")
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new { p.PaymentId, p.Amount, p.PaymentDate, p.PaymentMethod, p.PaymentCategory, p.TransactionReference, p.Status })
                    .ToListAsync();

                return Ok(new
                {
                    regNumber = student.RegNumber,
                    studentName = $"{student.FirstName} {student.LastName}",
                    programmeCode = student.ProgrammeCode,
                    programmeName = student.ProgrammeName,
                    semesterTuitionFee = semesterTuition,
                    tuitionPaid,
                    tuitionShortfall,
                    minimumTuitionRequired,
                    tuitionPercentagePaid = semesterTuition > 0 ? Math.Round((tuitionPaid / semesterTuition) * 100, 2) : 0,
                    functionalFeesPerYear,
                    functionalPaid,
                    functionalShortfall,
                    minimumFunctionalRequired,
                    functionalPercentagePaid = functionalFeesPerYear > 0 ? Math.Round((functionalPaid / functionalFeesPerYear) * 100, 2) : 0,
                    totalRequired,
                    totalPaid,
                    totalShortfall,
                    totalPercentagePaid = totalRequired > 0 ? Math.Round((totalPaid / totalRequired) * 100, 2) : 0,
                    minimumTotalRequired,
                    canEnroll,
                    has100PercentPaid,
                    enrollmentStatusMessage = canEnroll ? "✅ You have paid the minimum required amount and can enroll for courses" : $"❌ You need to pay at least UGX {Math.Max(0, minimumTuitionRequired - tuitionPaid)} more in tuition and UGX {Math.Max(0, minimumFunctionalRequired - functionalPaid)} more in functional fees to enroll",
                    paymentHistory = payments,
                    functionalFeesBreakdown = new { nationalCouncil = 20000, guildFees = 20000, scienceTechDev = 20000, libraryFee = 50000, developmentFee = 55000, medicalFees = 30000, studentIDCard = 10000, total = 205000 }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving fees: {ex.Message}" });
            }
        }


        [HttpGet("available-courses/{regNumber}")]
        public async Task<IActionResult> GetAvailableCourses(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);
                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                int currentYear = student.CurrentYear ?? 1;
                int currentSemester = student.CurrentSemester ?? 1;

                var enrolledCourseIds = await _context.CourseEnrollments
                    .Where(e => e.RegNumber == regNumber)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                var studentResults = await _context.Results
                    .Where(r => r.RegNumber == regNumber && r.Status == "RegistrarSignedOff")
                    .ToListAsync();

                var unresolvedFailedCourseIds = studentResults
                    .Where(r => (r.Grade ?? "F") == "F" &&
                                !(r.SupplementaryTaken == true && !string.IsNullOrEmpty(r.SupplementaryGrade) && r.SupplementaryGrade != "F"))
                    .Select(r => r.CourseId)
                    .Distinct()
                    .ToList();

                var normalCourses = await _context.Courses
                    .Where(c => c.IsActive == true &&
                                c.ProgrammeCode == student.ProgrammeCode &&
                                c.Year == currentYear &&
                                c.Semester == currentSemester &&
                                !enrolledCourseIds.Contains(c.CourseId))
                    .ToListAsync();

                var retakeCourses = await _context.Courses
                    .Where(c => unresolvedFailedCourseIds.Contains(c.CourseId) &&
                                c.Semester == currentSemester &&
                                !enrolledCourseIds.Contains(c.CourseId))
                    .ToListAsync();

                var combined = normalCourses
                    .Select(c => new
                    {
                        courseId = c.CourseId,
                        courseCode = c.CourseCode,
                        courseName = c.CourseName,
                        creditHours = c.CreditHours,
                        creditUnits = c.CreditHours,
                        year = c.Year,
                        semester = c.Semester,
                        description = c.Description,
                        isRetake = false
                    })
                    .Concat(retakeCourses.Select(c => new
                    {
                        courseId = c.CourseId,
                        courseCode = c.CourseCode,
                        courseName = c.CourseName,
                        creditHours = c.CreditHours,
                        creditUnits = c.CreditHours,
                        year = c.Year,
                        semester = c.Semester,
                        description = c.Description,
                        isRetake = true
                    }))
                    .OrderBy(c => c.isRetake).ThenBy(c => c.courseName)
                    .ToList();

                return Ok(new
                {
                    studentRegNumber = regNumber,
                    programmeName = student.ProgrammeName,
                    currentYear,
                    currentSemester,
                    courseCount = combined.Count,
                    courses = combined
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving courses: {ex.Message}" });
            }
        }

        [HttpPost("enroll")]
        public async Task<IActionResult> EnrollInCourse([FromBody] EnrollmentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.RegNumber) || request.CourseId <= 0)
                    return BadRequest(new { message = "RegNumber and CourseId are required" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == request.RegNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

               
                int? semesterIdToUseNullable = await GetCurrentGlobalSemesterIdAsync(request.RegNumber);
                if (semesterIdToUseNullable == null)
                {
                    return BadRequest(new
                    {
                        message = "No active semester enrollment found for this student. " +
                                   "This usually means the one-time enrollment backfill hasn't been run for them yet — contact the registrar."
                    });
                }
                int semesterIdToUse = semesterIdToUseNullable.Value;

                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;

                decimal minimumTuitionRequired = semesterTuition * 0.5m;
                decimal minimumFunctionalRequired = functionalFeesPerYear * 0.5m;

                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                decimal tuitionPaid = scoped.TuitionPaidThisSemester;
                decimal functionalPaid = scoped.FunctionalPaidThisYear;
                if (tuitionPaid < minimumTuitionRequired || functionalPaid < minimumFunctionalRequired)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "You must pay at least 50% of tuition and 50% of functional fees to enroll.",
                        feeBreakdown = new
                        {
                            tuitionRequirement = new { required = minimumTuitionRequired, paid = tuitionPaid, shortfall = Math.Max(0, minimumTuitionRequired - tuitionPaid) },
                            functionalRequirement = new { required = minimumFunctionalRequired, paid = functionalPaid, shortfall = Math.Max(0, minimumFunctionalRequired - functionalPaid) }
                        },
                        programmeName = student.ProgrammeName
                    });
                }

                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == request.CourseId);
                if (course == null) return NotFound(new { message = "Course not found" });


                bool isCurrentSemesterCourse = course.Year == student.CurrentYear && course.Semester == student.CurrentSemester;
                bool isEligibleRetake = false;
                if (!isCurrentSemesterCourse && course.Semester == student.CurrentSemester)
                {
                    var failedResult = await _context.Results.FirstOrDefaultAsync(r =>
                        r.RegNumber == request.RegNumber &&
                        r.CourseId == course.CourseId &&
                        r.Status == "RegistrarSignedOff" &&
                        (r.Grade ?? "F") == "F");
                    isEligibleRetake = failedResult != null &&
                        !(failedResult.SupplementaryTaken == true && !string.IsNullOrEmpty(failedResult.SupplementaryGrade) && failedResult.SupplementaryGrade != "F");
                }

                if (!isCurrentSemesterCourse && !isEligibleRetake)
                {
                    return BadRequest(new { message = "This course is not available for registration in your current semester." });
                }

                var existingEnrollment = await _context.CourseEnrollments
                    .FirstOrDefaultAsync(e => e.RegNumber == request.RegNumber && e.CourseId == request.CourseId);

                if (existingEnrollment != null)
                    return BadRequest(new { message = "Already enrolled in this course" });

                var enrollment = new Enrollment
                {
                    RegNumber = request.RegNumber,
                    CourseId = request.CourseId,
                    SemesterId = semesterIdToUse,
                    Status = "ENROLLED",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.CourseEnrollments.Add(enrollment);
                await _context.SaveChangesAsync();


                // ========== AUTO-GENERATE SEMESTER REGISTRATION CARD ==========
                string cardPath = "";
                try
                {
                    cardPath = await _cardService.GenerateSemesterRegistrationCard(student, semesterIdToUse);
                    Console.WriteLine($"[DEBUG] Card generated, path: {cardPath}");

                    if (!string.IsNullOrEmpty(cardPath))
                    {
                        var cardDocType = await _context.DocumentTypes
                            .FirstOrDefaultAsync(d =>
                                d.DocumentTypeName.Trim().ToUpper() == DocumentTypeNames.CourseRegistrationForm.ToUpper());

                        if (cardDocType == null)
                        {
                            Console.WriteLine(
                                $"[WARN] Document type '{DocumentTypeNames.CourseRegistrationForm}' not found in DocumentTypes table. " +
                                "Semester card PDF was generated but will NOT be saved to the database.");
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Attempting to save document...");

                            var document = new Document
                            {
                                RegNumber = student.RegNumber,
                                DocumentTypeId = cardDocType.DocumentTypeId,
                                DocumentType = cardDocType.DocumentTypeName,
                                DocumentPath = cardPath,
                                Status = "GENERATED",
                                DateGenerated = DateTime.Now,
                                DocumentFee = cardDocType.DocumentFee,
                                FeePaid = true,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            Console.WriteLine($"[DEBUG] Document object created: RegNumber={document.RegNumber}, Type={document.DocumentType}, Path={document.DocumentPath}");

                            _context.Documents.Add(document);
                            Console.WriteLine($"[DEBUG] Document added to context");

                            await _context.SaveChangesAsync();
                            Console.WriteLine($"✅ Semester card saved to DB: {cardPath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] Card path is empty!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Semester card generation/save failed: {ex.Message}");
                    Console.WriteLine($"❌ Inner exception: {ex.InnerException?.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");

                    _context.ChangeTracker.Clear();
                }
                // ========== SEND CARD EMAIL ==========
                try
                {
                    if (!string.IsNullOrEmpty(cardPath))
                        await _emailService.SendSemesterRegistrationCardAsync(student, cardPath, semesterIdToUse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Card email failed: {ex.Message}");
                }

                // ========== AUTO-GENERATE EXAM PERMIT (semester-aware) ==========
                string permitPath = "";

                try
                {
                    permitPath = await _permitService.GenerateExaminationPermit(student, semesterIdToUse);

                    if (!string.IsNullOrEmpty(permitPath))
                    {
                        await _emailService.SendExaminationPermitAsync(
                            student,
                            permitPath,
                            semesterIdToUse);
                        Console.WriteLine($"✅ Exam permit generated and emailed: {permitPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Exam permit generation/email failed: {ex.Message}");
                    Console.WriteLine($"❌ Inner exception: {ex.InnerException?.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");

                    _context.ChangeTracker.Clear();
                }

                return Ok(new
                {
                    success = true,
                    message = "Successfully enrolled in course",
                    courseCode = course.CourseCode,
                    courseName = course.CourseName,
                    creditHours = course.CreditHours,
                    enrollmentId = enrollment.EnrollmentId,
                    semesterCardGenerated = !string.IsNullOrEmpty(cardPath),
                    semesterCardPath = cardPath,
                    examinationPermitGenerated = !string.IsNullOrEmpty(permitPath),
                    examinationPermitPath = permitPath,
                    documentsSent = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Enrollment failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = $"Enrollment failed: {ex.Message}" });
            }
        }

        [HttpPost("enroll-batch")]
        public async Task<IActionResult> EnrollInCoursesBatch([FromBody] BatchEnrollmentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.RegNumber) || request.CourseIds == null || request.CourseIds.Count == 0)
                    return BadRequest(new { message = "RegNumber and at least one CourseId are required" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == request.RegNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

                // ===== AUTHORITATIVE SemesterId — same fix as EnrollInCourse =====
                int? semesterIdToUseNullable = await GetCurrentGlobalSemesterIdAsync(request.RegNumber);
                if (semesterIdToUseNullable == null)
                {
                    return BadRequest(new
                    {
                        message = "No active semester enrollment found for this student. " +
                                   "This usually means the one-time enrollment backfill hasn't been run for them yet — contact the registrar."
                    });
                }
                int semesterIdToUse = semesterIdToUseNullable.Value;

                // ===== FEE CLEARANCE CHECK — done ONCE for the whole batch =====
                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;

                decimal minimumTuitionRequired = semesterTuition * 0.5m;
                decimal minimumFunctionalRequired = functionalFeesPerYear * 0.5m;

                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                decimal tuitionPaid = scoped.TuitionPaidThisSemester;
                decimal functionalPaid = scoped.FunctionalPaidThisYear;

                if (tuitionPaid < minimumTuitionRequired || functionalPaid < minimumFunctionalRequired)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "You must pay at least 50% of tuition and 50% of functional fees to enroll.",
                        feeBreakdown = new
                        {
                            tuitionRequirement = new { required = minimumTuitionRequired, paid = tuitionPaid, shortfall = Math.Max(0, minimumTuitionRequired - tuitionPaid) },
                            functionalRequirement = new { required = minimumFunctionalRequired, paid = functionalPaid, shortfall = Math.Max(0, minimumFunctionalRequired - functionalPaid) }
                        },
                        programmeName = student.ProgrammeName
                    });
                }

                var alreadyEnrolledIds = await _context.CourseEnrollments
                    .Where(e => e.RegNumber == request.RegNumber)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                var distinctRequestedIds = request.CourseIds.Distinct().ToList();

                var registered = new List<object>();
                var failed = new List<object>();

                foreach (var courseId in distinctRequestedIds)
                {
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
                    if (course == null)
                    {
                        failed.Add(new { courseId, reason = "Course not found" });
                        continue;
                    }

                    if (alreadyEnrolledIds.Contains(courseId))
                    {
                        failed.Add(new { courseId, courseCode = course.CourseCode, reason = "Already enrolled" });
                        continue;
                    }

                    bool isCurrentSemesterCourse = course.Year == student.CurrentYear && course.Semester == student.CurrentSemester;
                    bool isEligibleRetake = false;

                    if (!isCurrentSemesterCourse && course.Semester == student.CurrentSemester)
                    {
                        var failedResult = await _context.Results.FirstOrDefaultAsync(r =>
                            r.RegNumber == request.RegNumber &&
                            r.CourseId == course.CourseId &&
                            r.Status == "RegistrarSignedOff" &&
                            (r.Grade ?? "F") == "F");
                        isEligibleRetake = failedResult != null &&
                            !(failedResult.SupplementaryTaken == true && !string.IsNullOrEmpty(failedResult.SupplementaryGrade) && failedResult.SupplementaryGrade != "F");
                    }

                    if (!isCurrentSemesterCourse && !isEligibleRetake)
                    {
                        failed.Add(new { courseId, courseCode = course.CourseCode, reason = "Not available for registration in your current semester" });
                        continue;
                    }

                    var enrollment = new Enrollment
                    {
                        RegNumber = request.RegNumber,
                        CourseId = courseId,
                        SemesterId = semesterIdToUse,
                        Status = "ENROLLED",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.CourseEnrollments.Add(enrollment);
                    registered.Add(new { courseId, courseCode = course.CourseCode, courseName = course.CourseName, creditHours = course.CreditHours });
                }

                if (registered.Count == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "None of the selected courses could be registered.",
                        registered,
                        failed
                    });
                }

                await _context.SaveChangesAsync();

                // ===== GENERATE DOCUMENTS ONCE FOR THE WHOLE BATCH =====
                string cardPath = "";
                try
                {
                    cardPath = await _cardService.GenerateSemesterRegistrationCard(student, semesterIdToUse);

                    if (!string.IsNullOrEmpty(cardPath))
                    {
                        var cardDocType = await _context.DocumentTypes
                            .FirstOrDefaultAsync(d =>
                                d.DocumentTypeName.Trim().ToUpper() == DocumentTypeNames.CourseRegistrationForm.ToUpper());

                        if (cardDocType != null)
                        {
                            var document = new Document
                            {
                                RegNumber = student.RegNumber,
                                DocumentTypeId = cardDocType.DocumentTypeId,
                                DocumentType = cardDocType.DocumentTypeName,
                                DocumentPath = cardPath,
                                Status = "GENERATED",
                                DateGenerated = DateTime.Now,
                                DocumentFee = cardDocType.DocumentFee,
                                FeePaid = true,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            _context.Documents.Add(document);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Semester card generation/save failed: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }

                try
                {
                    if (!string.IsNullOrEmpty(cardPath))
                        await _emailService.SendSemesterRegistrationCardAsync(student, cardPath, semesterIdToUse);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Card email failed: {ex.Message}");
                }

                string permitPath = "";
                try
                {
                    permitPath = await _permitService.GenerateExaminationPermit(student, semesterIdToUse);

                    if (!string.IsNullOrEmpty(permitPath))
                    {
                        await _emailService.SendExaminationPermitAsync(student, permitPath, semesterIdToUse);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Exam permit generation/email failed: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }

                return Ok(new
                {
                    success = true,
                    message = $"{registered.Count} of {distinctRequestedIds.Count} course(s) registered successfully.",
                    registered,
                    failed,
                    semesterCardGenerated = !string.IsNullOrEmpty(cardPath),
                    semesterCardPath = cardPath,
                    examinationPermitGenerated = !string.IsNullOrEmpty(permitPath),
                    examinationPermitPath = permitPath
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Batch enrollment failed: {ex.Message}");
                return StatusCode(500, new { message = $"Batch enrollment failed: {ex.Message}" });
            }
        }


        [HttpGet("results/{regNumber}")]
        public async Task<IActionResult> GetStudentResults(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);

                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                var programme = await _context.Programmes.FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);
                if (programme == null) return NotFound(new { message = "Programme not found" });

                decimal semesterTuition = FeeCategoryHelper.GetRequiredTuition(student, programme.TuitionFeePerSemester);
                decimal functionalFeesPerYear = FUNCTIONAL_FEES_PER_YEAR;

                decimal tuitionPaid = await _context.Payments
                    .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "TUITION" && p.Status == "Approved")
                    .SumAsync(p => p.Amount);

                decimal functionalPaid = await _context.Payments
                    .Where(p => p.RegNumber == regNumber && p.PaymentCategory == "FUNCTIONAL" && p.Status == "Approved")
                    .SumAsync(p => p.Amount);

                bool tuitionFullyPaid = tuitionPaid >= semesterTuition;
                bool functionalAtLeastHalf = functionalPaid >= (functionalFeesPerYear * 0.5m);
                bool functionalFullyPaid = functionalPaid >= functionalFeesPerYear;

                // Semester 1 gate: full tuition + >= 50% functional
                bool eligibleSemester1 = tuitionFullyPaid && functionalAtLeastHalf;
                // Semester 2 gate: full tuition + 100% functional
                bool eligibleSemester2 = tuitionFullyPaid && functionalFullyPaid;

                var signedOffResults = await _context.Results
                    .Where(r => r.RegNumber == regNumber && r.Status == "RegistrarSignedOff")
                    .OrderByDescending(r => r.RegistrarSignOffDate)
                    .ToListAsync();

                var visibleResults = signedOffResults
                    .Where(r => (r.Semester == 1 && eligibleSemester1) || (r.Semester == 2 && eligibleSemester2))
                    .Select(r => new
                    {
                        courseCode = r.CourseCode,
                        courseName = r.CourseName,
                        creditHours = r.CreditHours,
                        mark = r.Mark,
                        grade = r.Grade,
                        gradePoint = r.GradePoint,
                        semester = r.Semester,
                        year = r.Year,
                        signOffDate = r.RegistrarSignOffDate
                    })
                    .ToList();

                int hiddenCount = signedOffResults.Count - visibleResults.Count;

                return Ok(new
                {
                    results = visibleResults,
                    hiddenDueToFeeClearance = hiddenCount,
                    clearance = new
                    {
                        tuitionFullyPaid,
                        functionalAtLeastHalf,
                        functionalFullyPaid,
                        eligibleSemester1,
                        eligibleSemester2
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving results: {ex.Message}" });
            }
        }

        [HttpGet("documents/{regNumber}")]

        public async Task<IActionResult> GetStudentDocuments(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);

                var documentList = await _context.Documents
                    .Where(d => d.RegNumber == regNumber)
                    .OrderByDescending(d => d.DateGenerated)
                    .ToListAsync();

                var documents = documentList
                    .Select(d => new
                    {
                        documentId = d.DocumentId,
                        documentType = d.DocumentType,
                        documentPath = d.DocumentPath,
                        createdAt = d.DateGenerated,
                        documentName = GetDocumentName(d.DocumentType ?? string.Empty)
                    })
                    .ToList();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving documents: {ex.Message}" });
            }
        }

        [HttpGet("courses/{regNumber}")]
        public async Task<IActionResult> GetEnrolledCourses(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);
                var enrollments = await _context.CourseEnrollments
                    .Where(e => e.RegNumber == regNumber)
                    .Include(e => e.Course)
                    .OrderByDescending(e => e.SemesterId)
                    .Select(e => new
                    {
                        enrollmentId = e.EnrollmentId,
                        courseId = e.Course != null ? e.Course.CourseId : 0,
                        courseCode = e.Course != null ? e.Course.CourseCode : "",
                        courseName = e.Course != null ? e.Course.CourseName : "",
                        creditHours = e.Course != null ? e.Course.CreditHours : 0,
                        year = e.Course != null ? e.Course.Year : 0,
                        semester = e.Course != null ? e.Course.Semester : 0,
                        mark = e.Mark,
                        grade = e.Grade,
                        status = e.Status
                    })
                    .ToListAsync();
                return Ok(enrollments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving courses: {ex.Message}" });
            }
        }

        [HttpGet("{regNumber}")]
        public async Task<IActionResult> GetStudent(string regNumber)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);
                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                if (student == null) return NotFound(new { message = "Student not found" });
                return Ok(new
                {
                    student.RegNumber,
                    student.StudentId,
                    student.Email,
                    student.FirstName,
                    student.LastName,
                    student.ProgrammeCode,
                    student.ProgrammeName,
                    student.CurrentYear,
                    student.CurrentSemester,
                    student.Campus,
                    student.TotalPaid,
                    student.FeesBalance,
                    student.Status,
                    student.DateAdmitted
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving student: {ex.Message}" });
            }
        }

       
        [HttpPost("generate-exam-permit/{regNumber}")]
        public async Task<IActionResult> GenerateExamPermitOnDemand(string regNumber, [FromQuery] int? semesterId)
        {
            try
            {
                regNumber = System.Net.WebUtility.UrlDecode(regNumber);
                var student = await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == regNumber);
                if (student == null) return NotFound(new { message = "Student not found" });

                int resolvedSemesterId;
                if (semesterId.HasValue)
                {
                    resolvedSemesterId = semesterId.Value;
                }
                else
                {
                    var current = await GetCurrentGlobalSemesterIdAsync(regNumber);
                    if (current == null)
                    {
                        return BadRequest(new
                        {
                            message = "No active semester enrollment found for this student, and no semesterId was provided."
                        });
                    }
                    resolvedSemesterId = current.Value;
                }

                string permitPath = await _permitService.GenerateExaminationPermit(student, resolvedSemesterId);

                try
                {
                    await _emailService.SendExaminationPermitAsync(student, permitPath, resolvedSemesterId);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Exam permit generated but email failed for {Reg}", regNumber);
                }

                return Ok(new
                {
                    success = true,
                    message = "Examination permit generated successfully.",
                    semesterId = resolvedSemesterId,
                    examinationPermitPath = permitPath
                });
            }
            catch (Exception ex)
            {
                
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private static string GetDocumentName(string documentType)
        {
            return documentType switch
            {
                "CourseRegistrationForm" => "📚 Semester Registration Card",

                "ExamPermit" => "📋 Examination Permit",

                "SEMESTER_REGISTRATION_CARD" => "📚 Semester Registration Card",

                "EXAMINATION_PERMIT" => "📋 Examination Permit",

                "ADMISSION_LETTER" => "📜 Admission Letter",

                "TRANSCRIPT" => "📑 Academic Transcript",

                _ => $"📄 {documentType}"
            };
        }
    }

    public class EnrollmentRequest
    {
        public string? RegNumber { get; set; }
        public int CourseId { get; set; }
        public int? SemesterId { get; set; }
    }

    public class BatchEnrollmentRequest
    {
        public string? RegNumber { get; set; }
        public List<int> CourseIds { get; set; } = new();
        public int? SemesterId { get; set; }
    }
}