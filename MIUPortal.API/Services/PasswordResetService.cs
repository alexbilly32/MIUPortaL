using MIUPortal.API.Data;
using MIUPortal.API.Models;
using System.Net;
using System.Net.Mail;

namespace MIUPortal.API.Services
{
    public interface IPasswordResetService
    {
        Task<bool> SendResetEmailAsync(
            string email,
            string userRole,
            string token);
    }


    public class PasswordResetService : IPasswordResetService
    {
        private readonly MIUContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordResetService> _logger;


        public PasswordResetService(
            MIUContext context,
            IConfiguration configuration,
            ILogger<PasswordResetService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }



        public async Task<bool> SendResetEmailAsync(
            string email,
            string userRole,
            string token)
        {
            try
            {

                var smtpServer =
                    _configuration["Email:SmtpServer"];

                var smtpPort =
                    int.Parse(
                    _configuration["Email:SmtpPort"] ?? "587");

                var username =
                    _configuration["Email:Username"];

                var password =
                    _configuration["Email:Password"];


                if (string.IsNullOrEmpty(username) ||
                   string.IsNullOrEmpty(password))
                {
                    _logger.LogError(
                    "Email settings missing");

                    return false;
                }


                string resetLink =
                $"https://localhost:44366/reset-password.html?token={token}";



                string body = $@"

Dear MIU Portal User,

We received a request to reset your password.

Account Type:
{userRole}


Click the link below to create a new password:

{resetLink}


This link expires after 30 minutes.


If you did not request this password reset,
please ignore this email.


Regards,

MIU ICT Department
Metropolitan International University

";



                using var client =
                    new SmtpClient(
                        smtpServer,
                        smtpPort);


                client.EnableSsl = true;

                client.Credentials =
                    new NetworkCredential(
                        username,
                        password);



                using var message =
                    new MailMessage();


                message.From =
                    new MailAddress(
                        username,
                        "MIU Student Portal");


                message.To.Add(email);

                message.Subject =
                    "MIU Portal Password Reset";


                message.Body = body;


                await client.SendMailAsync(message);


                _logger.LogInformation(
                    $"Password reset email sent to {email}");

                return true;

            }
            catch (Exception ex)
            {
                _logger.LogError(
                ex,
                "Password reset email failed");

                return false;
            }

        }

    }
}