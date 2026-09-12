using System.Net;
using System.Net.Mail;

namespace MIUPortal.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string email, string firstName, string otpCode);
        Task<bool> SendApplicationStatusEmailAsync(string email, string firstName, string status);
        Task<bool> SendPaymentReminderEmailAsync(string email, string firstName, decimal amount);
        Task<bool> SendAdmissionLetterEmailAsync(string email, string firstName, string lastName, string regNumber, string pdfPath);
        Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetLink);
        Task<bool> SendResultsPublishedEmailAsync(string email, string firstName, string courseCode, string courseName, string grade);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // =========================
        // PUBLIC METHODS
        // =========================

        public async Task<bool> SendOtpEmailAsync(string email, string firstName, string otpCode)
        {
            string subject = "MIU Portal - OTP Verification";
            string body = GetOtpEmailTemplate(firstName, otpCode);

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendApplicationStatusEmailAsync(string email, string firstName, string status)
        {
            string subject = $"MIU Portal - Application {status}";
            string body = GetApplicationStatusTemplate(firstName, status);

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPaymentReminderEmailAsync(string email, string firstName, decimal amount)
        {
            string subject = "MIU Portal - Payment Reminder";
            string body = GetPaymentReminderTemplate(firstName, amount);

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAdmissionLetterEmailAsync(string email, string firstName, string lastName, string regNumber, string pdfPath)
        {
            string subject = $"MIU Admission Letter - {regNumber}";
            string body = GetAdmissionLetterEmailTemplate(firstName, regNumber);

            return await SendEmailWithAttachmentAsync(email, subject, body, pdfPath);
        }

        public async Task<bool> SendPasswordResetEmailAsync(
    string email,
    string firstName,
    string resetLink)
        {
            string subject = "MIU Portal - Password Reset";

            string body = $@"
    <html>
        <body style='font-family: Arial'>
            <h2>Hello {firstName},</h2>

            <p>You requested to reset your password.</p>

            <p>
                Click the link below to reset your password:
            </p>
<p>
    <a href='{resetLink}'
       style='background:#0b5345;
              color:white;
              padding:12px 20px;
              text-decoration:none;
              border-radius:5px;'>
        Reset Password
    </a>
</p>

<p>
If the button does not work, copy and paste this link into your browser:
</p>

<p>
{resetLink}
</p>

            <p>
                This link expires in 30 minutes.
            </p>

            <p>
                If you did not request this, please ignore this email.
            </p>

            <br>
            <p>MIU Portal Team</p>
        </body>
    </html>";

            return await SendEmailAsync(email, subject, body);
        }

       
        public async Task<bool> SendResultsPublishedEmailAsync(
            string email,
            string firstName,
            string courseCode,
            string courseName,
            string grade)
        {
            string subject = $"MIU Portal - Result Published: {courseCode}";
            string body = GetResultsPublishedTemplate(firstName, courseCode, courseName, grade);

            return await SendEmailAsync(email, subject, body);
        }

        // =========================
        // CORE SMTP EMAIL
        // =========================

        private async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpServer = _configuration["Email:SmtpServer"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:Username"];
                var smtpPassword = _configuration["Email:Password"];

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpUsername) ||
                    string.IsNullOrWhiteSpace(smtpPassword))
                {
                    _logger.LogError("SMTP configuration is missing in appsettings.json");
                    return false;
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 30000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUsername, "MIU Portal"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation($"Email sent successfully to {recipientEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {recipientEmail}");
                return false;
            }
        }

        // =========================
        // EMAIL WITH ATTACHMENT
        // =========================

        private async Task<bool> SendEmailWithAttachmentAsync(string recipientEmail, string subject, string htmlBody, string attachmentPath)
        {
            try
            {
                var smtpServer = _configuration["Email:SmtpServer"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:Username"];
                var smtpPassword = _configuration["Email:Password"];

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpUsername) ||
                    string.IsNullOrWhiteSpace(smtpPassword))
                {
                    _logger.LogError("SMTP configuration is missing in appsettings.json");
                    return false;
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = 30000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUsername, "MIU Admissions"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(recipientEmail);

                if (!string.IsNullOrWhiteSpace(attachmentPath))
                {
                    string fullPath = attachmentPath;

                    if (!Path.IsPathRooted(attachmentPath) || attachmentPath.StartsWith("/"))
                    {
                        string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        fullPath = Path.Combine(wwwrootPath, attachmentPath.TrimStart('/', '\\'));
                    }

                    if (File.Exists(fullPath))
                    {
                        mailMessage.Attachments.Add(new Attachment(fullPath));
                    }
                    else
                    {
                        _logger.LogWarning(
                            $"Attachment file not found, sending email WITHOUT attachment. " +
                            $"Original path: '{attachmentPath}', resolved to: '{fullPath}'");
                    }
                }
                else
                {
                    _logger.LogWarning("SendEmailWithAttachmentAsync called with an empty attachmentPath.");
                }

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation($"Email with attachment sent to {recipientEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send attachment email to {recipientEmail}");
                return false;
            }
        }

        // =========================
        // EMAIL TEMPLATES
        // =========================

        private string GetOtpEmailTemplate(string firstName, string otpCode)
        {
            return $@"
                <html>
                    <body>
                        <h2>Hello {firstName}</h2>
                        <p>Your OTP code is:</p>
                        <h1 style='letter-spacing:5px'>{otpCode}</h1>
                        <p>This code expires in 5 minutes.</p>
                    </body>
                </html>";
        }

        private string GetApplicationStatusTemplate(string firstName, string status)
            => $"<h2>{firstName}, your application status is: {status}</h2>";

        private string GetPaymentReminderTemplate(string firstName, decimal amount)
            => $"<h2>{firstName}, you owe UGX {amount:N0}</h2>";

        private string GetAdmissionLetterEmailTemplate(string firstName, string regNumber)
            => $"<h2>Congratulations {firstName}, Reg#: {regNumber}</h2>";

        private string GetResultsPublishedTemplate(string firstName, string courseCode, string courseName, string grade)
            => $@"
                <html>
                    <body style='font-family: Arial'>
                        <h2>Hello {firstName},</h2>
                        <p>Your result for <strong>{courseCode} - {courseName}</strong> has been published.</p>
                        <p style='font-size: 20px;'><strong>Grade: {grade}</strong></p>
                        <p>Log in to the MIU Portal to view your full results and GPA.</p>
                        <p>Note: results are only visible in your portal if you have met the required fee clearance for this semester.</p>
                        <br>
                        <p>MIU Portal Team</p>
                    </body>
                </html>";
    }
}