using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public class AdmissionLetterService
    {
        private readonly ILogger<AdmissionLetterService> _logger;
        private readonly string _pdfDirectory;
        private readonly MIUContext _context;
        private readonly QrCodeService _qrCodeService;

        public AdmissionLetterService(
            ILogger<AdmissionLetterService> logger,
            IConfiguration config,
            MIUContext context,
            QrCodeService qrCodeService)
        {
            _logger = logger;
            _context = context;
            _qrCodeService = qrCodeService;
            _pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "admission-letters");

            if (!Directory.Exists(_pdfDirectory))
            {
                Directory.CreateDirectory(_pdfDirectory);
                _logger.LogInformation($"Created admission-letters directory: {_pdfDirectory}");
            }
        }

        public async Task<string> GenerateAdmissionLetterPdf(Application application, Student student, AcademicSemester? admittingSemester = null)
        {
            {
                try
                {
                    if (student == null)
                        throw new ArgumentNullException(nameof(student));

                    if (string.IsNullOrEmpty(student.RegNumber))
                        throw new ArgumentException("Student RegNumber is required");

                    string sanitizedRegNumber = (student.RegNumber ?? "").Replace("/", "_");
                    string uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
                    string fileName = $"Admission_Letter_{sanitizedRegNumber}_{uniqueId}.pdf";
                    string filePath = Path.Combine(_pdfDirectory, fileName);

                    _logger.LogInformation($"Generating admission letter PDF: {fileName}");

  
                    var documentType = await _context.DocumentTypes
                        .FirstOrDefaultAsync(d =>
                            d.DocumentTypeName.Trim().ToUpper() == DocumentTypeNames.AdmissionLetter.ToUpper());

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
                    else
                    {
                        _logger.LogWarning(
                            $"Document type '{DocumentTypeNames.AdmissionLetter}' not found in DocumentTypes table. " +
                            "PDF will be generated WITHOUT a QR code and WILL NOT be saved to the database.");
                    }

                    await Task.Run(() =>
                    {
                        using (PdfWriter writer = new PdfWriter(filePath))
                        {
                            using (PdfDocument pdf = new PdfDocument(writer))
                            {
                                iText.Layout.Document document = new iText.Layout.Document(pdf);
                                document.SetMargins(35, 40, 35, 40);

                                var headerFont = PdfBrandingService.GetHeaderFont();
                                var bodyFont = PdfBrandingService.GetBodyFont();

                                // ========== BRANDED LETTERHEAD ==========
                                PdfBrandingService.AddLetterhead(document, "Office of the Academic Registrar");

                                // ========== STUDENT DETAILS BLOCK ==========
                                Table detailsTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2 }))
                                    .UseAllAvailableWidth()
                                    .SetMarginBottom(18);

                                PdfBrandingService.AddLabelValueRow(detailsTable, "Sur Name", student.LastName ?? "");
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Other Names", $"{student.FirstName ?? ""} {(student.MiddleName ?? "")}".Trim());
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Programme", GetProgrammeName(student.ProgrammeCode ?? ""));
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Faculty", GetFacultyName(student.ProgrammeCode ?? ""));
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Duration", "3 Years");
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Study Session", student.StudyMode ?? "");
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Campus", GetCampusName(student.CampusCode ?? ""));
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Level of Entry", "First Year");
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Admission Number", student.RegNumber ?? "");
                                PdfBrandingService.AddLabelValueRow(detailsTable, "Date", DateTime.Now.ToString("d MMMM, yyyy"));

                                document.Add(detailsTable);

                                // ========== GREETING + MAIN HEADING ==========
                                document.Add(new Paragraph($"Dear {student.FirstName},")
                                    .SetFont(bodyFont).SetFontSize(11).SetMarginBottom(12));

                                document.Add(new Paragraph("ADMISSION TO UNDERGRADUATE DEGREE PROGRAMME")
                                    .SetFont(headerFont)
                                    .SetFontSize(11)
                                    .SetFontColor(PdfBrandingService.MiuBlack)
                                    .SetMarginBottom(14));

                                // ========== BODY PARAGRAPH 1 ==========
                                string semesterStartText = admittingSemester?.StartDate != null
                                    ? admittingSemester.StartDate.Value.ToString("d MMMM, yyyy")
                                    : "the date communicated by the Academic Registrar";

                                document.Add(new Paragraph(
                                    $"I write to offer you an Admission at Metropolitan International University and the semester begins on {semesterStartText}. " +
                                    "You should ensure that you register with the university within two weeks from the beginning of the semester, unless prior " +
                                    "arrangements have been made to the contrary. Failure to do so will automatically lead to your place being forfeited to another candidate.")
                                    .SetFont(bodyFont).SetFontSize(10)
                                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                                    .SetMarginBottom(14));

                                // ========== REGISTRATION SECTION ==========
                                document.Add(PdfBrandingService.SectionTitle("REGISTRATION"));

                                document.Add(new Paragraph(
                                    "This is a provisional offer made on the basis of the statement of your qualifications presented on your application form. " +
                                    "It is subject to satisfactory verification of those qualifications by this office at the time of registration. " +
                                    "You must also present at the time of registration original documents and where possible evidence of those qualifications:\n\n" +
                                    "(i) Original Uganda Certificate of Education (or its equivalent) plus two photocopies of it.\n" +
                                    "(ii) Original Uganda Advanced Certificate of Education (or its equivalent)\n" +
                                    "(iii) Birth certificate or/and any National Identification documents.\n" +
                                    "(iv) Identity card from the previous school")
                                    .SetFont(bodyFont).SetFontSize(10)
                                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                                    .SetMarginBottom(14));

                                // ========== PAYMENT SECTION ==========
                                document.Add(PdfBrandingService.SectionTitle("PAYMENT"));

                                document.Add(new Paragraph(
                                    "After receiving this admission letter, obtain bank slip from the University Bursar and pay to the bank. " +
                                    "Return the slips to the University Bursar after banking and collect a clearance card from the same office. " +
                                    "No cash is allowed at the university.")
                                    .SetFont(bodyFont).SetFontSize(10)
                                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                                    .SetMarginBottom(14));

                                // ========== WELCOME PARAGRAPH ==========
                                document.Add(new Paragraph(
                                    "I congratulate you upon your admission at Metropolitan International University and on behalf of the university; " +
                                    "I extend you a warm welcome and wish you success in your studies here.")
                                    .SetFont(bodyFont).SetFontSize(10)
                                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                                    .SetMarginBottom(10));

                                // ========== SIGNATURE + STATUS STAMP ROW ==========
                                Table signRow = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                                    .UseAllAvailableWidth()
                                    .SetMarginTop(10);

                                Cell signCell = new Cell().SetBorder(Border.NO_BORDER);
                                signCell.Add(new Paragraph("Yours faithfully,\n\n\n").SetFont(bodyFont).SetFontSize(10).SetMarginBottom(0));
                                signCell.Add(new Paragraph("Academic Registrar\nMetropolitan International University").SetFont(bodyFont).SetFontSize(10));
                                signRow.AddCell(signCell);

                                Cell stampCell = new Cell().SetBorder(Border.NO_BORDER);
                                stampCell.Add(PdfBrandingService.StatusStamp("ADMITTED", "METROPOLITAN INT'L UNIVERSITY"));
                                signRow.AddCell(stampCell);

                                document.Add(signRow);

                                // ========== QR CODE =========
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
                                        .SetFont(bodyFont)
                                        .SetFontSize(7)
                                        .SetTextAlignment(TextAlignment.CENTER)
                                        .SetMarginTop(2));
                                }

                                // ========== FOOTER ==========
                                PdfBrandingService.AddFooter(document, "Office of the Academic Registrar");

                                document.Close();
                            }
                        }
                    });

                    _logger.LogInformation($"Admission letter PDF file created: {filePath}");

                    // =====================================
                    // SAVE ADMISSION LETTER TO DOCUMENTS TABLE
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

                        var document = new Document
                        {
                            RegNumber = student.RegNumber,
                            DocumentTypeId = documentType.DocumentTypeId,
                            DocumentType = documentType.DocumentTypeName,
                            DocumentPath = $"/admission-letters/{fileName}",
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
                            $"Admission letter saved to Documents table with embedded QR. DocumentId={document.DocumentId}");
                    }

                    return $"/admission-letters/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating admission letter PDF");
                    throw;
                }
            }
        }

        public string GetAdmissionLetterPath(string regNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(regNumber))
                {
                    _logger.LogWarning("Registration number is empty");
                    return "";
                }

                string sanitizedRegNumber = regNumber.Replace("/", "_");

                if (Directory.Exists(_pdfDirectory))
                {
                    var matchingFiles = Directory.GetFiles(_pdfDirectory, $"Admission_Letter_{sanitizedRegNumber}_*.pdf");

                    if (matchingFiles.Length > 0)
                    {
                        return matchingFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                    }
                }

                _logger.LogWarning($"No admission letter found for registration number: {regNumber}");
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting admission letter path for: {regNumber}");
                return "";
            }
        }

        public string GetProgrammeName(string code)
        {
            return code switch
            {
                "BIT" => "Bachelor of Information Technology",
                "BBA" => "Bachelor of Business Administration",
                "BED-ENG" => "Bachelor of Education - English",
                "BED-MATH" => "Bachelor of Education - Mathematics",
                "BED-SCI" => "Bachelor of Education - Science",
                "BMS" => "Bachelor of Medical Sciences",
                "BHM" => "Bachelor of Hospitality Management",
                "OPT" => "Bachelor of Optometry",
                "MED" => "Doctor of Medicine",
                "LLB" => "Bachelor of Laws",
                "MBA" => "Master of Business Administration",
                "MIT" => "Master of Information Technology",
                _ => code ?? "Programme"
            };
        }

        public string GetFacultyName(string code)
        {
            return code switch
            {
                "BIT" => "Science and Technology",
                "BBA" => "Business and Social Sciences",
                "BED-ENG" => "Education",
                "BED-MATH" => "Education",
                "BED-SCI" => "Education",
                "BMS" => "Health Sciences",
                "BHM" => "Business and Social Sciences",
                "OPT" => "Health Sciences",
                "MED" => "Health Sciences",
                "LLB" => "Law and Social Sciences",
                "MBA" => "Business and Social Sciences",
                "MIT" => "Science and Technology",
                _ => "General"
            };
        }

        public string GetCampusName(string code)
        {
            return code switch
            {
                "UMC" => "Kampala",
                "KSR" => "Kisoro",
                "MBR" => "Mbarara",
                _ => code ?? "Campus"
            };
        }
    }
}