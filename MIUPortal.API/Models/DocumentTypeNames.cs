namespace MIUPortal.API.Models
{
    /// <summary>
    /// Exact string values from the DocumentTypes.DocumentType column.
    /// ALWAYS use these constants instead of typing the string literal directly —
    /// this is what caused documents to silently fail to save (services were
    /// searching for "EXAMINATION_PERMIT" / "ADMISSION_LETTER" etc. while the
    /// DB actually stores "ExamPermit" / "AdmissionLetter").
    /// If you rename a row in the DB, update it here too — everything else
    /// references this file.
    /// </summary>
    public static class DocumentTypeNames
    {
        public const string AdmissionLetter = "AdmissionLetter";
        public const string StudentId = "StudentID";
        public const string CourseRegistrationForm = "CourseRegistrationForm";
        public const string ExamPermit = "ExamPermit";
        public const string Transcript = "Transcript";
        public const string CertificateOfEnrollment = "CertificateOfEnrollment";
        public const string IntroductionLetter = "IntroductionLetter";
        public const string RecommendationLetter = "RecommendationLetter";
        public const string ResultSlip = "ResultSlip";
        public const string ClearanceCertificate = "ClearanceCertificate";
        public const string PaymentReceipt = "PAYMENT_RECEIPT";
    }
}