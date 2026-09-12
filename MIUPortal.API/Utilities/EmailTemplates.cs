namespace MIUPortal.API.Utilities
{
    public class EmailTemplates
    {
        /// <summary>
        /// OTP verification email template
        /// </summary>
        public static string GetOtpTemplate(string studentName, string otp)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ background-color: #1a5f7a; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; }}
        .otp-box {{ background-color: #f0f0f0; border: 2px solid #1a5f7a; padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0; }}
        .otp-code {{ font-size: 32px; font-weight: bold; color: #1a5f7a; letter-spacing: 5px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Metropolitan International University</h1>
            <p>Student Portal - OTP Verification</p>
        </div>
        <div class=""content"">
            <p>Hello <strong>{studentName}</strong>,</p>
            <p>Your One-Time Password (OTP) for login is:</p>
            <div class=""otp-box"">
                <div class=""otp-code"">{otp}</div>
            </div>
            <p>This OTP will expire in <strong>5 minutes</strong>.</p>
            <p>If you did not request this OTP, please ignore this email.</p>
            <p>Best regards,<br>MIU Portal Support Team</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2026 Metropolitan International University-students' Final project. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Welcome email template
        /// </summary>
        public static string GetWelcomeTemplate(string studentName, string regNumber, string programme)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ background-color: #1a5f7a; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; }}
        .details {{ background-color: #f0f0f0; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Welcome to MIU Portal!</h1>
        </div>
        <div class=""content"">
            <p>Hello <strong>{studentName}</strong>,</p>
            <p>Welcome to Metropolitan International University Student Portal. Your account has been successfully activated.</p>
            <div class=""details"">
                <p><strong>Registration Number:</strong> {regNumber}</p>
                <p><strong>Programme:</strong> {programme}</p>
            </div>
            <p>You can now access the portal to:</p>
            <ul>
                <li>View your academic results</li>
                <li>Check fees status</li>
                <li>Download documents</li>
                <li>View your timetable</li>
                <li>Enroll in courses</li>
            </ul>
            <p>If you have any questions, please contact the admin team.</p>
            <p>Best regards,<br>MIU Portal Support Team</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2026 Metropolitan International University-students' Final project. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Payment confirmation email template
        /// </summary>
        public static string GetPaymentConfirmationTemplate(string studentName, decimal amount, string transactionRef)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; }}
        .receipt {{ background-color: #f0f0f0; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Payment Confirmation</h1>
        </div>
        <div class=""content"">
            <p>Hello <strong>{studentName}</strong>,</p>
            <p>Thank you for your payment. Your transaction has been confirmed.</p>
            <div class=""receipt"">
                <p><strong>Amount Paid:</strong> UGX {amount:N0}</p>
                <p><strong>Transaction Reference:</strong> {transactionRef}</p>
                <p><strong>Date:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
            </div>
            <p>Your fees account has been updated accordingly.</p>
            <p>If you have any questions, please contact the finance office.</p>
            <p>Best regards,<br>MIU Finance Team</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2026 Metropolitan International University-students' Final project. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Result notification email template
        /// </summary>
        public static string GetResultNotificationTemplate(string studentName, string courseName, string grade)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; }}
        .grade-box {{ background-color: #f0f0f0; padding: 15px; border-radius: 5px; margin: 20px 0; text-align: center; }}
        .grade {{ font-size: 28px; font-weight: bold; color: #007bff; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Result Notification</h1>
        </div>
        <div class=""content"">
            <p>Hello <strong>{studentName}</strong>,</p>
            <p>Your results for the following course have been published:</p>
            <p><strong>Course:</strong> {courseName}</p>
            <div class=""grade-box"">
                <p>Your Grade:</p>
                <div class=""grade"">{grade}</div>
            </div>
            <p>You can view full details and transcripts in the student portal.</p>
            <p>Best regards,<br>MIU Academic Office</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2026 Metropolitan International University-students' Final project. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}



