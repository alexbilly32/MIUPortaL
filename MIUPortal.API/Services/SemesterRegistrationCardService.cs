using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public class SemesterRegistrationCardService
    {
        private readonly ILogger<SemesterRegistrationCardService> _logger;
        private readonly string _pdfDirectory;
        private readonly MIUContext _context;
        private readonly QrCodeService _qrCodeService;
        private readonly FeeScopingService _feeScopingService;

        public SemesterRegistrationCardService(
            ILogger<SemesterRegistrationCardService> logger,
            IConfiguration config,
            MIUContext context,
            QrCodeService qrCodeService,
            FeeScopingService feeScopingService)

        {
            _logger = logger;
            _context = context;
            _qrCodeService = qrCodeService;
            _feeScopingService = feeScopingService;
            _pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "semester-cards");

            if (!Directory.Exists(_pdfDirectory))
            {
                Directory.CreateDirectory(_pdfDirectory);
            }

            _qrCodeService = qrCodeService;
        }

        public async Task<string> GenerateSemesterRegistrationCard(Student student, int semesterId)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    if (student == null)
                        throw new ArgumentNullException(nameof(student));

                    // Get enrolled courses
                    var enrolledCourses = await _context.CourseEnrollments
                        .Where(e => e.RegNumber == student.RegNumber && e.SemesterId == semesterId)
                        .Include(e => e.Course)
                        .ToListAsync();

                    
                    var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                    var tuitionPaid = scoped.TuitionPaidThisSemester;
                    var functionalPaid = scoped.FunctionalPaidThisYear;

                   
                    var semesterRecord = await _context.AcademicSemesters
                        .FirstOrDefaultAsync(s => s.SemesterId == semesterId);
                    string academicYearLabel = semesterRecord?.AcademicYear ?? "N/A";

                   
                    string campusDisplay = student.Campus ?? "N/A";
                    if (string.IsNullOrWhiteSpace(student.Campus) && !string.IsNullOrWhiteSpace(student.CampusCode))
                    {
                        var campusRecord = await _context.Campuses
                            .FirstOrDefaultAsync(c => c.CampusCode == student.CampusCode);
                        campusDisplay = campusRecord?.CampusName ?? student.CampusCode;
                    }

                   
                    string? absolutePhotoPath = !string.IsNullOrEmpty(student.PassportPhotoPath)
                        ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", student.PassportPhotoPath.TrimStart('/'))
                        : null;

                    string safeRegNumber = student.RegNumber != null
     ? student.RegNumber.Replace("/", "_")
     : "UnknownReg";

                    string uniqueId = Guid.NewGuid().ToString().Substring(0, 8);

                    string fileName =
                        $"SemesterCard_{safeRegNumber}_{semesterId}_{uniqueId}.pdf";

                    string filePath =
                        Path.Combine(_pdfDirectory, fileName);

                    // =====================================
                    // RESOLVE DOCUMENT TYPE & PREPARE QR DATA
                    // =====================================

                    var documentType = await _context.DocumentTypes
                        .FirstOrDefaultAsync(d =>
                            d.DocumentTypeName.Trim().ToUpper() ==
                            "COURSEREGISTRATIONFORM");

                    string? documentNumber = null;
                    string? verificationId = null;
                    string? verificationHash = null;
                    string? verificationUrl = null;
                    byte[]? qrBytes = null;
                    string? qrFileName = null;

                    var qrFolder = Path.Combine(
    Directory.GetCurrentDirectory(),
    "wwwroot",
    "uploads",
    "qrcodes");

                    if (!Directory.Exists(qrFolder))
                    {
                        Directory.CreateDirectory(qrFolder);
                    }

                    if (qrBytes != null && !string.IsNullOrEmpty(documentNumber))
                    {
                        qrFileName = $"{documentNumber}.png";
                        var qrPath = Path.Combine(qrFolder, qrFileName);
                        await File.WriteAllBytesAsync(qrPath, qrBytes);
                    }
                    if (documentType != null)
                    {
                        documentNumber =
                            _qrCodeService.GenerateDocumentNumber(
                                documentType.DocumentTypeName);

                        verificationId =
                            _qrCodeService.GenerateVerificationId();

                        verificationHash =
                            _qrCodeService.GenerateVerificationHash(
                                verificationId);

                        verificationUrl =
                            _qrCodeService.BuildVerificationUrl(
                                verificationId);

                        qrBytes =
                            _qrCodeService.GenerateQrImage(
                                verificationUrl);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "DocumentType 'CourseRegistrationForm' not found.");
                    }

                    using (PdfWriter writer = new PdfWriter(filePath))
                    {
                        using (PdfDocument pdf = new PdfDocument(writer))
                        {
                            iText.Layout.Document document = new iText.Layout.Document(pdf);
                            document.SetMargins(25, 25, 25, 25);

                            PdfFont boldFont = PdfBrandingService.GetHeaderFont();
                            PdfFont regularFont = PdfBrandingService.GetBodyFont();

                            // ========== BRANDED LETTERHEAD ==========
                            PdfBrandingService.AddLetterhead(document, string.Empty);

                            document.Add(new Paragraph("SEMESTER REGISTRATION CARD")
                                .SetFont(boldFont)
                                .SetFontSize(13)
                                .SetFontColor(PdfBrandingService.MiuRed)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetMarginBottom(16));

                            // ========== STUDENT INFORMATION TABLE ==========
                            document.Add(PdfBrandingService.SectionTitle("STUDENT INFORMATION"));

                            Table studentTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1, 1, 1 }))
                                .UseAllAvailableWidth();

                            AddTableCell(studentTable, "Registration Number", student.RegNumber ?? "N/A");
                            AddTableCell(studentTable, "Full Name", $"{student.FirstName} {student.LastName}");
                            AddTableCell(studentTable, "Programme", student.ProgrammeName ?? "N/A");
                            AddTableCell(studentTable, "Campus", campusDisplay);
                            AddTableCell(studentTable, "Email", student.Email ?? "N/A");
                            AddTableCell(studentTable, "Semester ID", semesterId.ToString());
                            AddTableCell(studentTable, "Registration Date", DateTime.Now.ToString("dd/MM/yyyy"));
                            AddTableCell(studentTable, "Academic Year", academicYearLabel);

                            
                            Table detailsWithPhoto = new Table(UnitValue.CreatePercentArray(new float[] { 74, 26 }))
                                .UseAllAvailableWidth()
                                .SetMarginBottom(16);

                            detailsWithPhoto.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(studentTable));
                            detailsWithPhoto.AddCell(PdfBrandingService.StudentPhotoCell(absolutePhotoPath));

                            document.Add(detailsWithPhoto);

                            // ========== PAYMENT STATUS ==========
                            document.Add(PdfBrandingService.SectionTitle("PAYMENT STATUS"));

                            Table paymentTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 }))
                                .UseAllAvailableWidth()
                                .SetMarginBottom(16);

                            paymentTable.AddCell(PdfBrandingService.TableHeaderCell("Fee Category"));
                            paymentTable.AddCell(PdfBrandingService.TableHeaderCell("Amount Paid (UGX)"));
                            paymentTable.AddCell(PdfBrandingService.TableHeaderCell("Status"));

                            paymentTable.AddCell(PdfBrandingService.TableBodyCell("Tuition"));
                            paymentTable.AddCell(PdfBrandingService.TableBodyCell(tuitionPaid.ToString("N0"), TextAlignment.RIGHT));
                            paymentTable.AddCell(PdfBrandingService.TableBodyCell(tuitionPaid > 0 ? "Received" : "Pending", TextAlignment.CENTER));

                            paymentTable.AddCell(PdfBrandingService.TableBodyCell("Functional Fees"));
                            paymentTable.AddCell(PdfBrandingService.TableBodyCell(functionalPaid.ToString("N0"), TextAlignment.RIGHT));
                            paymentTable.AddCell(PdfBrandingService.TableBodyCell(functionalPaid > 0 ? "Received" : "Pending", TextAlignment.CENTER));

                            decimal totalPaid = tuitionPaid + functionalPaid;
                            paymentTable.AddCell(new Cell()
                                .Add(new Paragraph("TOTAL").SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE))
                                .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));
                            paymentTable.AddCell(new Cell()
                                .Add(new Paragraph(totalPaid.ToString("N0")).SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE).SetTextAlignment(TextAlignment.RIGHT))
                                .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));
                            paymentTable.AddCell(new Cell()
                                .Add(new Paragraph("Semester Payment").SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE).SetTextAlignment(TextAlignment.CENTER))
                                .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));

                            document.Add(paymentTable);

                            // ========== REGISTERED COURSES ==========
                            document.Add(PdfBrandingService.SectionTitle("REGISTERED COURSES"));

                            if (enrolledCourses.Count > 0)
                            {
                                Table coursesTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2.5f, 1, 0.7f, 1 }))
                                    .UseAllAvailableWidth()
                                    .SetMarginBottom(10);

                                coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Code"));
                                coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Course Name"));
                                coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Credit Hrs"));
                                coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Year"));
                                coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Semester"));

                                int totalCredits = 0;
                                foreach (var enrollment in enrolledCourses.OrderBy(e => e.Course?.CourseCode ?? ""))
                                {
                                    if (enrollment.Course != null)
                                    {
                                        coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.CourseCode ?? ""));
                                        coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.CourseName ?? ""));
                                        coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.CreditHours.ToString(), TextAlignment.CENTER));
                                        coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.Year.ToString(), TextAlignment.CENTER));
                                        coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.Semester.ToString(), TextAlignment.CENTER));
                                        totalCredits += enrollment.Course.CreditHours;
                                    }
                                }

                                document.Add(coursesTable);

                                document.Add(new Paragraph($"Total Credit Hours: {totalCredits}")
                                    .SetFont(boldFont)
                                    .SetFontSize(10)
                                    .SetMarginBottom(14));
                            }
                            else
                            {
                                document.Add(new Paragraph("No courses registered for this semester.")
                                    .SetFont(regularFont)
                                    .SetFontSize(10)
                                    .SetMarginBottom(14));
                            }

                            // =====================================
                            // QR CODE
                            // =====================================

                            if (qrBytes != null)
                            {
                                var qrImageData =
                                    ImageDataFactory.Create(qrBytes);

                                var qrImage =
                                    new Image(qrImageData)
                                        .SetWidth(65)
                                        .SetHeight(65)
                                        .SetHorizontalAlignment(
                                            HorizontalAlignment.CENTER);

                                document.Add(qrImage);

                                document.Add(

                                    new Paragraph(
                                        $"Scan to verify authenticity · Doc No: {documentNumber}")
                                    .SetFont(regularFont)
                                    .SetFontSize(7)
                                    .SetTextAlignment(TextAlignment.CENTER)
                                    .SetMarginTop(2)

                                );
                            }

                            // ========== FOOTER ==========
                            document.Add(new Paragraph("This is an official semester registration card from Metropolitan International University. " +
                                "Present this card during course attendance and examinations.")
                                .SetFont(regularFont)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetFontSize(8)
                                .SetMarginTop(14)
                                .SetMarginBottom(2));

                            PdfBrandingService.AddFooter(document, $"Generated on: {DateTime.Now:dd MMMM yyyy HH:mm:ss}");

                            document.Close();
                        }
                    }

                    // =====================================
                    // SAVE REGISTRATION CARD TO DOCUMENTS TABLE
                    // =====================================

                    var documentType2 = await _context.DocumentTypes
                        .FirstOrDefaultAsync(d => d.DocumentTypeName == "CourseRegistrationForm");

                    if (documentType2 != null)
                    {
                        string documentPath = $"/semester-cards/{fileName}";

                        bool exists = await _context.Documents.AnyAsync(d =>
                            d.RegNumber == student.RegNumber &&
                            d.DocumentTypeId == documentType2.DocumentTypeId &&
                            d.DocumentPath == documentPath);

                        if (!exists)
                        {
                            if (documentType == null)
                            {
                                _logger.LogWarning("DocumentType 'CourseRegistrationForm' was not found when saving document.");
                                return $"/semester-cards/{fileName}";
                            }

                            var generatedDocument = new Document
                            {
                                RegNumber = student.RegNumber,
                                DocumentTypeId = documentType.DocumentTypeId,
                                DocumentType = documentType.DocumentTypeName,
                                DocumentPath = documentPath,
                                Status = "GENERATED",
                                DateGenerated = DateTime.Now,
                                DocumentFee = documentType.DocumentFee,
                                FeePaid = true,

                                DocumentNumber = documentNumber,
                                VerificationId = verificationId,
                                VerificationHash = verificationHash,
                                QrCodePath = qrFileName != null ? $"uploads/qrcodes/{qrFileName}" : null,

                                ExpiryDate = DateTime.Now.AddMonths(6),
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            _context.Documents.Add(generatedDocument);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation(
                                $"Registration card saved to Documents table. DocumentId={generatedDocument.DocumentId}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("DocumentType 'CourseRegistrationForm' was not found.");
                    }

                    _logger.LogInformation($"Semester registration card generated: {fileName}");
                    return $"/semester-cards/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating semester registration card: {ex.Message}");
                    throw;
                }
            });
        }

        private void AddTableCell(Table table, string label, string value)
        {
            label = label ?? "N/A";
            value = value ?? "N/A";

            table.AddCell(new Cell()
                .Add(new Paragraph(label).SetFont(PdfBrandingService.GetHeaderFont()).SetFontSize(8))
                .SetBackgroundColor(PdfBrandingService.LightGrey)
                .SetPadding(5));
            table.AddCell(new Cell()
                .Add(new Paragraph(value).SetFont(PdfBrandingService.GetBodyFont()).SetFontSize(9))
                .SetPadding(5));
        }
    }
}