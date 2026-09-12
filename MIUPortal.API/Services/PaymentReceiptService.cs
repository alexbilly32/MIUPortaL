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
    public class PaymentReceiptService
    {
        private readonly ILogger<PaymentReceiptService> _logger;
        private readonly string _pdfDirectory;
        private readonly MIUContext _context;
        private readonly QrCodeService _qrCodeService;

        public PaymentReceiptService(
            ILogger<PaymentReceiptService> logger,
            IConfiguration config,
            MIUContext context,
            QrCodeService qrCodeService)
        {
            _logger = logger;
            _context = context;
            _qrCodeService = qrCodeService;
            _pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "payment-receipts");

            if (!Directory.Exists(_pdfDirectory))
            {
                Directory.CreateDirectory(_pdfDirectory);
            }
        }

        public async Task<string> GeneratePaymentReceipt(Payment payment)
        {
            try
            {
                if (payment == null)
                    throw new ArgumentNullException(nameof(payment));

                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == payment.RegNumber);

                if (student == null)
                    throw new Exception("Student not found");

                var documentType = await _context.DocumentTypes
                    .FirstOrDefaultAsync(d =>
                        d.DocumentTypeName.Trim().ToUpper() == DocumentTypeNames.PaymentReceipt.ToUpper());

                if (documentType == null)
                {
                    _logger.LogWarning(
                        $"Document type '{DocumentTypeNames.PaymentReceipt}' not found in DocumentTypes table. " +
                        "PDF will be generated WITHOUT a QR code and WILL NOT be saved to the database.");
                }

                Document? existingReceipt = null;
                if (documentType != null)
                {
                    existingReceipt = await _context.Documents
                        .FirstOrDefaultAsync(d =>
                            d.PaymentId == payment.PaymentId &&
                            d.DocumentTypeId == documentType.DocumentTypeId);
                }

                if (existingReceipt != null)
                {
                    _logger.LogInformation(
                        $"Payment receipt already exists for PaymentId={payment.PaymentId}. DocumentId={existingReceipt.DocumentId}");

                    payment.ReceiptPath = existingReceipt.DocumentPath;
                    payment.ReceiptGenerated = true;
                    await _context.SaveChangesAsync();

                    return existingReceipt.DocumentPath ?? "";
                }

                string fileName = $"Receipt_{(payment.TransactionReference ?? "UNKNOWN").Replace("/", "_")}.pdf";
                string filePath = Path.Combine(_pdfDirectory, fileName);

                // =====================================
                // PREPARE QR DATA BEFORE building the PDF
                // =====================================
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

                await Task.Run(() =>
                {
                    using (PdfWriter writer = new PdfWriter(filePath))
                    {
                        using (PdfDocument pdf = new PdfDocument(writer))
                        {
                            iText.Layout.Document document = new iText.Layout.Document(pdf);
                            document.SetMargins(30, 35, 30, 35);

                            PdfFont boldFont = PdfBrandingService.GetHeaderFont();
                            PdfFont regularFont = PdfBrandingService.GetBodyFont();

                           Table topRow = new Table(UnitValue.CreatePercentArray(new float[] { 2.2f, 1 }))
                                .UseAllAvailableWidth()
                                .SetMarginBottom(4);

                            Cell letterheadCell = new Cell().SetBorder(Border.NO_BORDER);
                            if (File.Exists("wwwroot/assets/branding/miu-crest.png"))
                            {
                                var crestImg = new Image(ImageDataFactory.Create("wwwroot/assets/branding/miu-crest.png"))
                                    .SetWidth(38);
                                letterheadCell.Add(crestImg);
                            }
                            letterheadCell.Add(new Paragraph("METROPOLITAN INTERNATIONAL UNIVERSITY")
                                .SetFont(boldFont).SetFontSize(11).SetMarginTop(2).SetMarginBottom(1));
                            letterheadCell.Add(new Paragraph("Plot 281, Namungona Hoima Highway, Kampala or P.O. Box 160 Kisoro\nTel: +256 772 561957 | Email: metropolitanu@gmail.com")
                                .SetFont(regularFont).SetFontSize(7));
                            topRow.AddCell(letterheadCell);

                            Cell receiptBoxCell = new Cell()
                                .SetBorder(new SolidBorder(PdfBrandingService.MiuBlack, 1.2f))
                                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetPadding(10)
                                .Add(new Paragraph("GENERAL RECEIPT").SetFont(boldFont).SetFontSize(12));
                            topRow.AddCell(receiptBoxCell);

                            document.Add(topRow);

                            document.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f))
                                .SetMarginBottom(14));

                            // ========== DOTTED-FIELD RECEIPT INFO ==========
                            document.Add(DottedField("Date", DateTime.Now.ToString("dd MMMM yyyy"), regularFont));
                            document.Add(DottedField("Receipt No.", payment.TransactionReference ?? "N/A", regularFont));
                            document.Add(DottedField("Received with thanks from", $"{student.FirstName} {student.LastName}", regularFont));
                            document.Add(DottedField("Registration No.", student.RegNumber ?? "N/A", regularFont));
                            document.Add(DottedField("The sum of shillings",
                                NumberToWordsUgx(payment.Amount) + " Only", regularFont));
                            document.Add(DottedField("Being payment for",
                                $"{payment.PaymentCategory ?? "Fee"} Payment", regularFont));

                            document.Add(new Paragraph(" ").SetMarginBottom(4));

                            // ========== PAYMENT METHOD MINI-TABLE ==========
                            Table methodBox = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                                .UseAllAvailableWidth()
                                .SetBorder(new SolidBorder(PdfBrandingService.MiuBlack, 1f))
                                .SetMarginBottom(16);

                            methodBox.AddCell(PdfBrandingService.TableBodyCell("Payment Method: " + (payment.PaymentMethod ?? "Bank Transfer")));
                            methodBox.AddCell(PdfBrandingService.TableBodyCell("Provider / Bank: " + (payment.Network ?? "N/A")));
                            methodBox.AddCell(PdfBrandingService.TableBodyCell("Payment Date: " + payment.PaymentDate.ToString("dd MMM yyyy HH:mm")));
                            methodBox.AddCell(PdfBrandingService.TableBodyCell(
                                string.IsNullOrEmpty(payment.PhoneNumber) ? "" : "Phone: " + payment.PhoneNumber));

                            document.Add(methodBox);

                            // ========== TOTAL AMOUNT BLOCK ==========
                            Table totalTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                                .UseAllAvailableWidth()
                                .SetMarginBottom(18);

                            totalTable.AddCell(new Cell()
                                .Add(new Paragraph("TOTAL AMOUNT PAID").SetFont(boldFont).SetFontSize(11).SetFontColor(ColorConstants.WHITE))
                                .SetBackgroundColor(PdfBrandingService.MiuGreen)
                                .SetPadding(8));
                            totalTable.AddCell(new Cell()
                                .Add(new Paragraph($"UGX {payment.Amount:N0}").SetFont(boldFont).SetFontSize(11).SetFontColor(ColorConstants.WHITE).SetTextAlignment(TextAlignment.RIGHT))
                                .SetBackgroundColor(PdfBrandingService.MiuGreen)
                                .SetPadding(8));

                            document.Add(totalTable);

                            // ========== SIGNATURE LINE ==========
                            document.Add(new Paragraph("Signature: ..........................................    For Bursar, Metropolitan International University")
                                .SetFont(regularFont).SetFontSize(9).SetMarginBottom(6));

                            // ========== IMPORTANT NOTES ==========
                            document.Add(PdfBrandingService.SectionTitle("IMPORTANT INFORMATION").SetFontSize(9).SetMarginTop(10));

                            string[] notes = new[]
                            {
                                "This receipt serves as proof of payment for the amount specified above.",
                                "Please keep this receipt for your records and future reference.",
                                "The student can download their semester registration card and examination permit from the student portal once payment is verified.",
                                "For inquiries or disputes regarding this payment, contact the Finance Office immediately."
                            };
                            foreach (var note in notes)
                            {
                                document.Add(new Paragraph("• " + note)
                                    .SetFont(regularFont).SetFontSize(8).SetMarginBottom(3));
                            }

                            // ========== QR CODE ==========
                            if (qrBytes != null)
                            {
                                var qrImageData = ImageDataFactory.Create(qrBytes);
                                var qrImage = new Image(qrImageData)
                                    .SetWidth(60)
                                    .SetHeight(60)
                                    .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                                    .SetMarginTop(14);
                                document.Add(qrImage);

                                document.Add(new Paragraph($"Scan to verify authenticity · Doc No: {documentNumber}")
                                    .SetFont(regularFont).SetFontSize(7)
                                    .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(2));
                            }

                            // ========== FOOTER ==========
                            PdfBrandingService.AddFooter(document, "Finance Office | Metropolitan International University");

                            document.Close();
                        }
                    }
                });

                _logger.LogInformation($"Payment receipt PDF file created: {fileName}");

                string receiptPath = $"/payment-receipts/{fileName}";

                // =====================================
                // SAVE PAYMENT RECEIPT TO DOCUMENTS TABLE
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

                    var newDocument = new Document
                    {
                        PaymentId = payment.PaymentId,
                        RegNumber = payment.RegNumber,
                        DocumentTypeId = documentType.DocumentTypeId,
                        DocumentType = documentType.DocumentTypeName,
                        DocumentPath = receiptPath,
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

                    _context.Documents.Add(newDocument);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        $"Payment receipt saved to Documents table with embedded QR. DocumentId={newDocument.DocumentId}");
                }

                payment.ReceiptPath = receiptPath;
                payment.ReceiptGenerated = true;
                await _context.SaveChangesAsync();

                return receiptPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Payment receipt generation failed for PaymentId {PaymentId}",
                    payment?.PaymentId);

                throw;
            }
        }

        private Paragraph DottedField(string label, string value, PdfFont font)
        {
            int dotsNeeded = Math.Max(3, 60 - label.Length - (value?.Length ?? 0));
            string dots = new string('.', Math.Min(dotsNeeded, 40));

            return new Paragraph()
                .Add(new Text($"{label}: ").SetFont(PdfBrandingService.GetHeaderFont()).SetFontSize(9))
                .Add(new Text($"{value} ").SetFont(font).SetFontSize(9))
                .Add(new Text(dots).SetFont(font).SetFontSize(9).SetFontColor(PdfBrandingService.MidGrey))
                .SetMarginBottom(8);
        }

        private string NumberToWordsUgx(decimal amount)
        {
            try
            {
                long value = (long)amount;
                if (value == 0) return "Zero shillings";
                if (value > 999_999_999) return $"{amount:N0} shillings";

                string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
                    "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

                string ConvertHundreds(long n)
                {
                    string result = "";
                    if (n >= 100)
                    {
                        result += ones[n / 100] + " Hundred ";
                        n %= 100;
                    }
                    if (n >= 20)
                    {
                        result += tens[n / 10] + " ";
                        n %= 10;
                    }
                    if (n > 0)
                    {
                        result += ones[n] + " ";
                    }
                    return result.Trim();
                }

                string words = "";
                if (value >= 1_000_000)
                {
                    words += ConvertHundreds(value / 1_000_000) + " Million ";
                    value %= 1_000_000;
                }
                if (value >= 1_000)
                {
                    words += ConvertHundreds(value / 1_000) + " Thousand ";
                    value %= 1_000;
                }
                if (value > 0)
                {
                    words += ConvertHundreds(value);
                }

                return words.Trim() + " Shillings";
            }
            catch
            {
                return $"{amount:N0} Shillings";
            }
        }
    }
}