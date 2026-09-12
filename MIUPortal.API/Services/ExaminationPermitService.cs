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
    public class ExaminationPermitService
    {
        private readonly ILogger<ExaminationPermitService> _logger;
        private readonly string _pdfDirectory;
        private readonly MIUContext _context;
        private readonly QrCodeService _qrCodeService;
        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

        private readonly FeeScopingService _feeScopingService;

        public ExaminationPermitService(
    ILogger<ExaminationPermitService> logger,
    IConfiguration config,
    MIUContext context,
    QrCodeService qrCodeService,
    FeeScopingService feeScopingService)
        {
            _logger = logger;
            _context = context;
            _qrCodeService = qrCodeService;
            _feeScopingService = feeScopingService;
            _pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "exam-permits");
        }

        public async Task<string> GenerateExaminationPermit(Student student, int semesterId)
        {
            try
            {
                if (student == null)
                    throw new ArgumentNullException(nameof(student));

                if (string.IsNullOrEmpty(student.RegNumber))
                    throw new ArgumentException("Student RegNumber is required");

                var programme = await _context.Programmes
                     .FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);

                if (programme == null)
                    throw new Exception("Programme not found");


                var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
                var tuitionPaid = scoped.TuitionPaidThisSemester;
                var functionalPaid = scoped.FunctionalPaidThisYear;

                decimal tuitionRequired = programme.TuitionFeePerSemester;

                var semester = await _context.AcademicSemesters
                    .FirstOrDefaultAsync(s => s.SemesterId == semesterId);

                if (semester == null)
                {
                    throw new Exception("Academic semester not found.");
                }


                int studentCurrentSemester = student.CurrentSemester ?? 1;
                decimal functionalRequired = studentCurrentSemester == 1
                    ? FUNCTIONAL_FEES_PER_YEAR * 0.5m
                    : FUNCTIONAL_FEES_PER_YEAR;

                if (tuitionPaid < tuitionRequired)
                {
                    throw new Exception(
                        $"Cannot generate permit. Tuition paid: {tuitionPaid:N0}/{tuitionRequired:N0}");
                }

                if (functionalPaid < functionalRequired)
                {
                    throw new Exception(
                        $"Cannot generate permit. Functional fees paid: {functionalPaid:N0}/{functionalRequired:N0}");
                }
                decimal totalPaid = tuitionPaid + functionalPaid;

                
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

                var enrolledCourses = await _context.CourseEnrollments
                    .Where(e => e.RegNumber == student.RegNumber && e.SemesterId == semesterId)
                    .Include(e => e.Course)
                    .ToListAsync();

                string examCardNumber = GenerateExamCardNumber(student.RegNumber);
                string fileName = $"ExamPermit_{student.RegNumber.Replace("/", "_")}_{semesterId}.pdf";
                string filePath = Path.Combine(_pdfDirectory, fileName);

                var documentType = await _context.DocumentTypes
                    .FirstOrDefaultAsync(d =>
                        d.DocumentTypeName.Trim().ToUpper() == DocumentTypeNames.ExamPermit.ToUpper());

                if (documentType == null)
                {
                    _logger.LogWarning($"Document type '{DocumentTypeNames.ExamPermit}' not found.");
                }

                
                Document? priorPermit = null;
                if (documentType != null)
                {
                    priorPermit = await _context.Documents
                        .Where(d => d.RegNumber == student.RegNumber &&
                                    d.DocumentTypeId == documentType.DocumentTypeId &&
                                    d.DocumentPath == $"/exam-permits/{fileName}" &&
                                    !d.IsRevoked)
                        .OrderByDescending(d => d.DateGenerated)
                        .FirstOrDefaultAsync();
                }

                string? documentNumber = null;
                string? verificationId = null;
                string? verificationHash = null;
                string? verificationUrl = null;
                byte[]? qrBytes = null;

                if (documentType != null)
                {
                    documentNumber = _qrCodeService.GenerateDocumentNumber(documentType.DocumentTypeName);
                    verificationId = _qrCodeService.GenerateVerificationId();
                    verificationHash = _qrCodeService.GenerateVerificationHash(verificationId);
                    verificationUrl = _qrCodeService.BuildVerificationUrl(verificationId);
                    qrBytes = _qrCodeService.GenerateQrImage(verificationUrl);
                }

                if (!Directory.Exists(_pdfDirectory))
                {
                    Directory.CreateDirectory(_pdfDirectory);
                }

                using (var writer = new PdfWriter(filePath))
                {
                    using (var pdf = new PdfDocument(writer))
                    {
                        iText.Layout.Document document = new iText.Layout.Document(pdf);
                        document.SetMargins(25, 25, 25, 25);

                        PdfFont boldFont = PdfBrandingService.GetHeaderFont();
                        PdfFont regularFont = PdfBrandingService.GetBodyFont();

                        // ========== BRANDED LETTERHEAD ==========
                        PdfBrandingService.AddLetterhead(document, "Office of the Academic Registrar");

                        // ========== PERMIT TITLE BLOCK ==========
                        
                        document.Add(new Paragraph($"EXAMINATION PERMIT {semester.AcademicYear}")
                            .SetFont(boldFont)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontSize(14)
                            .SetMarginBottom(2)
                            .SetFontColor(PdfBrandingService.MiuRed));

                        document.Add(new Paragraph("END OF SEMESTER UNIVERSITY EXAMINATIONS")
                            .SetFont(boldFont)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontSize(11)
                            .SetMarginBottom(2));

                        document.Add(new Paragraph("This permit and a valid registration card must be presented to the invigilator before entry for every examination paper.")
                            .SetFont(regularFont)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontSize(8)
                            .SetMarginBottom(16));

                        // ========== EXAM CARD + STUDENT DETAILS ==========
                        Table examDetails = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1, 1, 1 }))
                            .UseAllAvailableWidth();

                        AddDetailCell(examDetails, "Exam Card No.", examCardNumber ?? "N/A");
                        AddDetailCell(examDetails, "Reg. No.", student.RegNumber ?? "N/A");
                        AddDetailCell(examDetails, "Year of Study", student.CurrentYear.HasValue ? student.CurrentYear.Value.ToString() : "N/A");
                        AddDetailCell(examDetails, "Semester", (student.CurrentSemester ?? 1).ToString());
                        AddDetailCell(examDetails, "Name (as on previous academic documents)", $"{student.FirstName} {student.LastName}");
                        AddDetailCell(examDetails, "Programme", student.ProgrammeName ?? "N/A");
                        AddDetailCell(examDetails, "Campus", campusDisplay);
                        AddDetailCell(examDetails, "Issue Date", DateTime.Now.ToString("dd MMMM yyyy"));

                       
                        Table detailsWithPhoto = new Table(UnitValue.CreatePercentArray(new float[] { 74, 26 }))
                            .UseAllAvailableWidth()
                            .SetMarginBottom(16);

                        detailsWithPhoto.AddCell(new Cell().SetBorder(Border.NO_BORDER).SetPadding(0).Add(examDetails));
                        detailsWithPhoto.AddCell(PdfBrandingService.StudentPhotoCell(absolutePhotoPath));

                        document.Add(detailsWithPhoto);

                        // ========== PAYMENT VERIFICATION ==========
                        document.Add(PdfBrandingService.SectionTitle("PAYMENT VERIFICATION"));

                        Table paymentInfo = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 }))
                            .UseAllAvailableWidth()
                            .SetMarginBottom(16);

                        paymentInfo.AddCell(PdfBrandingService.TableHeaderCell("Fee Type"));
                        paymentInfo.AddCell(PdfBrandingService.TableHeaderCell("Amount Paid (UGX)"));
                        paymentInfo.AddCell(PdfBrandingService.TableHeaderCell("Status"));

                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell("Tuition"));
                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell(tuitionPaid.ToString("N0"), TextAlignment.RIGHT));
                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell("Verified", TextAlignment.CENTER));

                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell("Functional Fees"));
                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell(functionalPaid.ToString("N0"), TextAlignment.RIGHT));
                        paymentInfo.AddCell(PdfBrandingService.TableBodyCell("Verified", TextAlignment.CENTER));

                        paymentInfo.AddCell(new Cell()
                            .Add(new Paragraph("TOTAL PAID").SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE))
                            .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));
                        paymentInfo.AddCell(new Cell()
                            .Add(new Paragraph(totalPaid.ToString("N0")).SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE).SetTextAlignment(TextAlignment.RIGHT))
                            .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));
                        paymentInfo.AddCell(new Cell()
                            .Add(new Paragraph("Complete").SetFont(boldFont).SetFontSize(9).SetFontColor(ColorConstants.WHITE).SetTextAlignment(TextAlignment.CENTER))
                            .SetBackgroundColor(PdfBrandingService.MiuGreen).SetPadding(5));

                        document.Add(paymentInfo);

                        // ========== COURSE UNITS REGISTERED (matches real exam card layout) ==========
                        document.Add(PdfBrandingService.SectionTitle("COURSE UNITS REGISTERED"));

                        if (enrolledCourses.Count > 0)
                        {
                            Table coursesTable = new Table(UnitValue.CreatePercentArray(new float[] { 0.5f, 1, 2.3f, 1.3f, 0.9f, 0.9f }))
                                .UseAllAvailableWidth()
                                .SetMarginBottom(10);

                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("#"));
                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Code"));
                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Course Name"));
                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Invigilator"));
                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Date"));
                            coursesTable.AddCell(PdfBrandingService.TableHeaderCell("Sign"));

                            int courseNum = 1;
                            foreach (var enrollment in enrolledCourses.OrderBy(e => e.Course?.CourseCode ?? ""))
                            {
                                if (enrollment.Course != null)
                                {
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell(courseNum.ToString(), TextAlignment.CENTER));
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell(enrollment.Course.CourseCode ?? ""));
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell(
                                        enrollment.Course.CourseName + (enrollment.Status == "RETAKE" ? "  (Retake)" : "")));
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell("", TextAlignment.CENTER));
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell("", TextAlignment.CENTER));
                                    coursesTable.AddCell(PdfBrandingService.TableBodyCell("", TextAlignment.CENTER));
                                    courseNum++;
                                }
                            }

                            document.Add(coursesTable);
                        }
                        else
                        {
                            document.Add(new Paragraph("No courses registered for this semester.")
                                .SetFont(regularFont).SetFontSize(10).SetMarginBottom(10));
                        }

                        // ========== IMPORTANT NOTICES ==========
                        document.Add(PdfBrandingService.SectionTitle("IMPORTANT NOTICES").SetFontSize(9));

                        string[] notices = new[]
                        {
                            "This permit authorizes you to sit for examinations only if all fees are paid in full.",
                            "Produce this permit during examination registration and in the examination hall.",
                            "Examination dates will be communicated separately by the Academic Registrar.",
                            "Any discrepancies should be reported to the Academic Registrar immediately.",
                            "This card is not valid without official stamp and Registrar signature."
                        };
                        foreach (var notice in notices)
                        {
                            document.Add(new Paragraph("• " + notice)
                                .SetFont(regularFont).SetFontSize(8).SetMarginBottom(3));
                        }

                        // ========== SIGNATURE + STAMP ROW ==========
                        Table signRow = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                            .UseAllAvailableWidth()
                            .SetMarginTop(14);

                        Cell signCell = new Cell().SetBorder(Border.NO_BORDER);
                        signCell.Add(new Paragraph("Academic Registrar's Signature: ..........................................")
                            .SetFont(regularFont).SetFontSize(9));
                        signRow.AddCell(signCell);

                        Cell stampCell = new Cell().SetBorder(Border.NO_BORDER);
                        stampCell.Add(PdfBrandingService.StatusStamp("VALID", "ACADEMIC REGISTRAR"));
                        signRow.AddCell(stampCell);

                        document.Add(signRow);

                        // ========== QR CODE (embedded, only if we have one) ==========
                        if (qrBytes != null)
                        {
                            var qrImageData = ImageDataFactory.Create(qrBytes);
                            var qrImage = new Image(qrImageData)
                                .SetWidth(65)
                                .SetHeight(65)
                                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                                .SetMarginTop(14);
                            document.Add(qrImage);

                            document.Add(new Paragraph($"Scan to verify authenticity · Doc No: {documentNumber}")
                                .SetFont(regularFont)
                                .SetFontSize(7)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetMarginTop(2));
                        }

                        // ========== FOOTER ==========
                        PdfBrandingService.AddFooter(document, "Office of the Academic Registrar | Metropolitan International University");

                        document.Close();
                    }
                }

                // =====================================
                // SAVE EXAMINATION PERMIT TO DOCUMENTS TABLE
                // =====================================
                if (documentType != null && qrBytes != null)
                {
                    var qrFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "qrcodes");
                    if (!Directory.Exists(qrFolder))
                    {
                        Directory.CreateDirectory(qrFolder);
                    }

                    var qrFileName = $"{documentNumber}.png";
                    var qrPath = Path.Combine(qrFolder, qrFileName);
                    await File.WriteAllBytesAsync(qrPath, qrBytes);

                    
                    if (priorPermit != null)
                    {
                        priorPermit.IsRevoked = true;
                        priorPermit.RevokedAt = DateTime.Now;
                    }

                    var document = new Document
                    {
                        RegNumber = student.RegNumber,
                        DocumentTypeId = documentType.DocumentTypeId,
                        DocumentType = documentType.DocumentTypeName,
                        DocumentPath = $"/exam-permits/{fileName}",
                        Status = "GENERATED",
                        DateGenerated = DateTime.Now,
                        DocumentFee = documentType.DocumentFee,
                        FeePaid = true,
                        DocumentNumber = documentNumber,
                        VerificationId = verificationId,
                        VerificationHash = verificationHash,
                        QrCodePath = $"uploads/qrcodes/{qrFileName}",
                        ExpiryDate = DateTime.Now.AddMonths(6),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Documents.Add(document);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                     "Saved Document ID: {0}, Path: {1}",
                     document.DocumentId,
                     document.DocumentPath
                    );

                    _logger.LogInformation(
                        $"Exam permit saved to Documents table with embedded QR. DocumentId={document.DocumentId}");
                }

                _logger.LogInformation($"Examination permit generated: {fileName}");

                return $"/exam-permits/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating examination permit");
                throw;
            }
        }

        private string GenerateExamCardNumber(string regNumber)
        {
            if (string.IsNullOrEmpty(regNumber))
                return "EXAM-UNKNOWN";

            var parts = regNumber.Split('/');
            string year = DateTime.Now.Year.ToString();
            string sequence = regNumber.GetHashCode().ToString().Substring(0, 4).PadLeft(4, '0');
            return $"EXAM-{year}-{(parts.Length > 1 ? parts[1] : "XX")}-{sequence}";
        }

        private void AddDetailCell(Table table, string label, string value)
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