using MIUPortal.API.Models;
using System.Net;
using System.Net.Mail;

namespace MIUPortal.API.Services
{
    public interface IDocumentEmailService
    {
        Task<bool> SendPaymentReceiptAsync(Student student, Payment payment, string receiptPath);
        Task<bool> SendSemesterRegistrationCardAsync(Student student, string cardPath, int semesterId);
        Task<bool> SendExaminationPermitAsync(Student student, string permitPath, int semesterId);
        Task<bool> SendDocumentAsync(string studentEmail, string documentPath, string documentType, string subject);
    }

    public class DocumentEmailService : IDocumentEmailService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<DocumentEmailService> _logger;
        private readonly IConfiguration _config;

        public DocumentEmailService(IEmailService emailService, ILogger<DocumentEmailService> logger, IConfiguration config)
        {
            _emailService = emailService;
            _logger = logger;
            _config = config;
        }

        public async Task<bool> SendPaymentReceiptAsync(Student student, Payment payment, string receiptPath)
        {
            try
            {
                if (student == null)
                {
                    _logger.LogWarning("Student is null in SendPaymentReceiptAsync");
                    return false;
                }

                if (payment == null)
                {
                    _logger.LogWarning("Payment is null in SendPaymentReceiptAsync");
                    return false;
                }

                string subject = $"Payment Receipt - {payment.TransactionReference ?? "UNKNOWN"}";
                string body = $@"
Dear {student.FirstName} {student.LastName},

Your payment has been processed successfully.

Payment Details:
- Transaction Reference: {payment.TransactionReference ?? "N/A"}
- Amount: UGX {payment.Amount:N0}
- Category: {payment.PaymentCategory ?? "N/A"}
- Payment Method: {payment.PaymentMethod ?? "N/A"}
- Date: {payment.PaymentDate:dd MMMM yyyy HH:mm:ss}

Your payment receipt is attached to this email. Please keep it for your records.

The receipt confirms your payment and can be used for enrollment verification.

Best regards,
MIU Finance Office
Metropolitan International University
";

                return await SendDocumentAsync(student.Email ?? "", receiptPath ?? "", "Payment Receipt", subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending payment receipt: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendSemesterRegistrationCardAsync(Student student, string cardPath, int semesterId)
        {
            try
            {
                if (student == null)
                {
                    _logger.LogWarning("Student is null in SendSemesterRegistrationCardAsync");
                    return false;
                }

                string subject = $"Semester Registration Card - Semester {semesterId}";
                string body = $@"
Dear {student.FirstName} {student.LastName},

Your semester registration card for Semester {semesterId} has been generated and is ready for download.

Card Details:
- Student: {student.FirstName} {student.LastName}
- Registration Number: {student.RegNumber ?? "N/A"}
- Programme: {student.ProgrammeName ?? "N/A"}
- Semester: {semesterId}
- Generated: {DateTime.Now:dd MMMM yyyy}

Your semester registration card is attached. This card shows:
✓ Your registered courses for the semester
✓ Credit hours for each course
✓ Your payment status
✓ Important examination information

Please download and keep this card. You will need to present it during:
- Course attendance verification
- Examination registration
- In the examination hall

If you have any discrepancies, please contact the Academic Registrar immediately.

Best regards,
Academic Registrar
Metropolitan International University
";

                return await SendDocumentAsync(student.Email ?? "", cardPath ?? "", "Semester Registration Card", subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending semester registration card: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendExaminationPermitAsync(Student student, string permitPath, int semesterId)
        {
            try
            {
                if (student == null)
                {
                    _logger.LogWarning("Student is null in SendExaminationPermitAsync");
                    return false;
                }

                string subject = $"Examination Permit - Semester {semesterId} - OFFICIAL";
                string body = $@"
Dear {student.FirstName} {student.LastName},

CONGRATULATIONS! Your examination permit has been issued.

This is official authorization for you to sit for examinations in Semester {semesterId}.

Permit Details:
- Student: {student.FirstName} {student.LastName}
- Registration Number: {student.RegNumber ?? "N/A"}
- Programme: {student.ProgrammeName ?? "N/A"}
- Exam Semester: {semesterId}
- Issued: {DateTime.Now:dd MMMM yyyy}

✓ ALL FEES PAID IN FULL
✓ ELIGIBLE FOR EXAMINATIONS

Your examination permit is attached. This permit authorizes you to:
• Sit for examinations in all registered courses
• Register for examinations during the scheduled registration period
• Enter the examination hall during your scheduled exam times

IMPORTANT:
- Bring this permit to examination registration
- Present it during each examination
- Any discrepancies must be reported to the Academic Registrar
- Examination dates and timetable will be sent separately

Examination dates will be communicated by the Academic Registrar. Please check your email and the student portal regularly.

Best regards,
Academic Registrar
Metropolitan International University
";

                return await SendDocumentAsync(student.Email ?? "", permitPath ?? "", "Examination Permit", subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending examination permit: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendDocumentAsync(string studentEmail, string documentPath, string documentType, string subject, string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(studentEmail))
                {
                    _logger.LogWarning($"Student email is null or empty for {documentType}");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(documentPath))
                {
                    _logger.LogWarning($"Document path is null or empty for {documentType}");
                    return false;
                }

                // Get the full file path
                string wwwrootPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot");
                string fullPath = System.IO.Path.Combine(wwwrootPath, documentPath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning($"Document file not found: {fullPath}");
                    return false;
                }

                // ✅ UPDATED: Use matching config keys
                var smtpServer = _config["Email:SmtpServer"];
                var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var smtpUsername = _config["Email:Username"];
                var smtpPassword = _config["Email:Password"];

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpUsername) ||
                    string.IsNullOrWhiteSpace(smtpPassword))
                {
                    _logger.LogError("Email configuration is missing");
                    return false;
                }

                // Send email with attachment
                using (SmtpClient client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    client.Timeout = 30000;

                    using (MailMessage message = new MailMessage())
                    {
                        message.From = new MailAddress(smtpUsername, "MIU Student Portal");
                        message.To.Add(studentEmail);
                        message.Subject = subject;
                        message.Body = body;
                        message.IsBodyHtml = false;

                        // Add attachment
                        message.Attachments.Add(new Attachment(fullPath));

                        await client.SendMailAsync(message);
                        _logger.LogInformation($"Document email sent: {documentType} to {studentEmail}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending document email: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");  // ← Added for debugging
                return false;
            }
        }

        public async Task<bool> SendDocumentAsync(string studentEmail, string documentPath, string documentType, string subject)
        {
            return await SendDocumentAsync(studentEmail, documentPath, documentType, subject, "");
        }
    }
}
