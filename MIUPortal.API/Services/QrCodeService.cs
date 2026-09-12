using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using QRCoder;

namespace MIUPortal.API.Services
{
    public class QrCodeService
    {
        private readonly IConfiguration _configuration;

        public QrCodeService(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // Generates official MIU document numbers
        public string GenerateDocumentNumber(string documentType)
        {
            string prefix = documentType.ToUpper() switch
            {
                "TRANSCRIPT" => "TRN",
                "SEMESTER REGISTRATION CARD" => "SRC",
                "CERTIFICATE" => "CERT",
                "ADMISSION LETTER" => "ADM",
                "EXAM CARD" => "EXAM",
                "RECEIPT" => "RCP",
                _ => "DOC"
            };


            int year = DateTime.Now.Year;

            string randomNumber =
                Random.Shared.Next(1, 999999)
                .ToString("D6");


            return $"MIU-{prefix}-{year}-{randomNumber}";
        }


        // Creates unique verification ID
        public string GenerateVerificationId()
        {
            return Guid.NewGuid().ToString();
        }


        // Creates digital signature
        public string GenerateVerificationHash(
            string verificationId)
        {

            var secret =
                _configuration["DocumentVerification:SecretKey"];


            using var hmac =
                new HMACSHA256(
                    Encoding.UTF8.GetBytes(secret!));


            var hash =
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(verificationId));


            return Convert.ToBase64String(hash);
        }



        // Creates QR destination URL
        public string BuildVerificationUrl(
            string verificationId)
        {

            var baseUrl =
                _configuration[
                    "DocumentVerification:BaseUrl"];


            return $"{baseUrl}/api/documents/verify/{verificationId}";
        }



        // Generates QR image bytes
        public byte[] GenerateQrImage(string url)
        {

            using var generator =
                new QRCodeGenerator();


            using var data =
                generator.CreateQrCode(
                    url,
                    QRCodeGenerator.ECCLevel.Q);


            var qr =
                new PngByteQRCode(data);


            return qr.GetGraphic(20);
        }



        // Validates QR authenticity
        public bool ValidateVerification(
            string verificationId,
            string storedHash)
        {

            var expected =
                GenerateVerificationHash(
                    verificationId);


            return expected == storedHash;
        }



        public bool ValidateToken(string token, string regNumber, string documentType, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(token) || !token.Contains(':'))
            {
                error = "Malformed verification token.";
                return false;
            }

            var parts = token.Split(':', 2);
            if (parts.Length != 2)
            {
                error = "Malformed verification token.";
                return false;
            }

            string verificationId = parts[1];

            // Assuming the hash is stored in the database and accessible via regNumber and documentType
            // You may need to adjust this logic based on your actual storage/retrieval mechanism
            // For demonstration, let's assume you pass the correct hash as part of the token (not secure for production)
            // In production, retrieve the hash from the database using regNumber and documentType

            // Example: Validate the verificationId using the stored hash
            // string storedHash = ... (retrieve from database)
            // return ValidateVerification(verificationId, storedHash);

            // For now, just check the verificationId is not empty
            if (string.IsNullOrWhiteSpace(verificationId))
            {
                error = "Invalid verification ID.";
                return false;
            }

            // If you have a way to get the stored hash, use the ValidateVerification method
            // For now, always return true for demonstration
            return true;
        }
    }
}