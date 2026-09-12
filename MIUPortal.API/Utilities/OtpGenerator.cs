namespace MIUPortal.API.Utilities
{
    public class OtpGenerator
    {
        private static readonly Random Random = new Random();

        /// <summary>
        /// Generate a random OTP code (6 digits)
        /// </summary>
        public static string GenerateOtp()
        {
            return Random.Next(100000, 999999).ToString();
        }

        /// <summary>
        /// Generate OTP with custom length
        /// </summary>
        public static string GenerateOtp(int length)
        {
            if (length < 4)
                length = 4;

            int min = (int)Math.Pow(10, length - 1);
            int max = (int)Math.Pow(10, length) - 1;

            return Random.Next(min, max).ToString();
        }

        /// <summary>
        /// Check if OTP is expired
        /// </summary>
        public static bool IsOtpExpired(DateTime? expiryTime)
        {
            return expiryTime == null || expiryTime < DateTime.Now;
        }

        /// <summary>
        /// Get OTP expiry time (default 5 minutes from now)
        /// </summary>
        public static DateTime GetOtpExpiry(int minutesValid = 5)
        {
            return DateTime.Now.AddMinutes(minutesValid);
        }
    }
}
