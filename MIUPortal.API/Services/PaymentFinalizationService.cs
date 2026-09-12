using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;
using MIUPortal.API.Utilities;

namespace MIUPortal.API.Services
{
    public class PaymentFinalizationResult
    {
        public int PaymentId { get; set; }
        public string? TransactionReference { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentCategory { get; set; }
        public string? StudentRegNumber { get; set; }
        public string? StudentName { get; set; }
        public string? ReceiptPath { get; set; }
        public bool ReceiptGenerated { get; set; }
        public bool ReceiptEmailSent { get; set; }
        public decimal TuitionRequired { get; set; }
        public decimal TuitionPaid { get; set; }
        public decimal TuitionRemaining { get; set; }
        public decimal FunctionalRequired { get; set; }
        public decimal FunctionalPaid { get; set; }
        public decimal FunctionalRemaining { get; set; }
        public decimal TotalRequired { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRemaining { get; set; }
        public bool CanEnroll { get; set; }
        public string EnrollmentStatus { get; set; } = "";
        public string? Status { get; set; }
    }


    public class PaymentFinalizationService
    {
        private readonly MIUContext _context;
        private readonly PaymentReceiptService _receiptService;
        private readonly IDocumentEmailService _emailService;
        private readonly SemesterRegistrationCardService _semesterCardService;
        private readonly ExaminationPermitService _examPermitService;
        private readonly FeeScopingService _feeScopingService;
        private const decimal FUNCTIONAL_FEES_PER_YEAR = 205000m;

        public PaymentFinalizationService(
            MIUContext context,
            PaymentReceiptService receiptService,
            IDocumentEmailService emailService,
            SemesterRegistrationCardService semesterCardService,
            ExaminationPermitService examPermitService,
            FeeScopingService feeScopingService)
        {
            _context = context;
            _receiptService = receiptService;
            _emailService = emailService;
            _semesterCardService = semesterCardService;
            _examPermitService = examPermitService;
            _feeScopingService = feeScopingService;
        }

        public async Task<PaymentFinalizationResult> FinalizeApprovedPaymentAsync(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                throw new InvalidOperationException($"Payment {paymentId} not found");

            if (payment.Status != "Approved")
                throw new InvalidOperationException(
                    $"Payment {paymentId} has Status='{payment.Status}', expected 'Approved'. " +
                    "Caller must set Status to Approved and save before finalizing.");

            var student = payment.Student
                ?? await _context.Students.FirstOrDefaultAsync(s => s.RegNumber == payment.RegNumber);
            if (student == null)
                throw new InvalidOperationException($"Student {payment.RegNumber} not found");

            var programme = await _context.Programmes
                .FirstOrDefaultAsync(p => p.ProgrammeCode == student.ProgrammeCode);

            AcademicSemester? semester = payment.SemesterId.HasValue
                ? await _context.AcademicSemesters.FirstOrDefaultAsync(s => s.SemesterId == payment.SemesterId)
                : null;

            // ============ LEDGER ENTRY ============

            var ledgerEntry = new FinancialTransaction
            {
                RegNumber = payment.RegNumber,
                TransactionType = "PAYMENT",
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                PaymentReference = payment.TransactionReference,
                TransactionDate = DateTime.Now,
                Status = "COMPLETED",
                Notes = payment.ProcessedByBursarId.HasValue
                    ? $"{payment.PaymentCategory} payment (verified by bursar)"
                    : $"{payment.PaymentCategory} payment via student portal",
                Payment = payment,
                BursarId = payment.ProcessedByBursarId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.FinancialLedger.Add(ledgerEntry);

            // ============ BALANCE RECALCULATION ============

            var scoped = await _feeScopingService.GetScopedPaymentsAsync(student);
            decimal newTuitionPaid = scoped.TuitionPaidThisSemester;
            decimal newFunctionalPaid = scoped.FunctionalPaidThisYear;
            decimal newCurrentPeriodPaid = newTuitionPaid + newFunctionalPaid;


            decimal lifetimeTotalPaid = await _context.Payments
                .Where(p => p.RegNumber == payment.RegNumber && p.Status == "Approved")
                .SumAsync(p => p.Amount);

            decimal semesterTuition = programme?.TuitionFeePerSemester ?? 0;


            int studentCurrentSemester = student.CurrentSemester ?? 1;
            decimal functionalRequired = studentCurrentSemester == 1
                ? FUNCTIONAL_FEES_PER_YEAR * 0.5m
                : FUNCTIONAL_FEES_PER_YEAR;

           
            decimal tuitionRequired = FeeCategoryHelper.GetRequiredTuition(student, semesterTuition);

            student.TotalPaid = lifetimeTotalPaid;

            student.FeesBalance = Math.Max(0, (tuitionRequired + functionalRequired) - newCurrentPeriodPaid);
            student.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // ============ RECEIPT ============
            string receiptPath = "";
            bool receiptEmailSent = false;

            try
            {
                receiptPath = await _receiptService.GeneratePaymentReceipt(payment);
                payment.ReceiptPath = receiptPath;
                payment.ReceiptGenerated = true;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Receipt generation FAILED: " + ex);
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(receiptPath))
                {
                    receiptEmailSent = await _emailService.SendPaymentReceiptAsync(student, payment, receiptPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Receipt email failed: {ex.Message}");
            }

            // ============ ELIGIBILITY & DOCUMENT GENERATION ============
            bool tuitionOk = newTuitionPaid >= tuitionRequired;
            bool functionalOk = newFunctionalPaid >= functionalRequired;
            bool eligibleForRegistrationCard = tuitionOk && functionalOk && semester != null;

            string regCardPath = "";
            string examPermitPath = "";

            try
            {
                if (eligibleForRegistrationCard && payment.SemesterId.HasValue)
                {
                    regCardPath = await _semesterCardService.GenerateSemesterRegistrationCard(student, payment.SemesterId.Value);

                    if (!string.IsNullOrWhiteSpace(student.RegNumber))
                    {
                        if (!string.IsNullOrWhiteSpace(regCardPath))
                            await SaveDocument(student.RegNumber, "SEMESTER_REGISTRATION_CARD", regCardPath);

                        if (!string.IsNullOrWhiteSpace(examPermitPath))
                            await SaveDocument(student.RegNumber, "EXAMINATION_PERMIT", examPermitPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Document generation error: " + ex.Message);
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    if (!string.IsNullOrWhiteSpace(regCardPath))
                        await _emailService.SendDocumentAsync(student.Email, regCardPath, "SEMESTER_REGISTRATION_CARD", "Semester Registration Card");

                    if (!string.IsNullOrWhiteSpace(examPermitPath))
                        await _emailService.SendDocumentAsync(student.Email, examPermitPath, "EXAMINATION_PERMIT", "Examination Permit");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Email notification failed: {ex.Message}");
            }

            decimal minimumTuitionRequired = tuitionRequired * 0.5m;
            decimal minimumFunctionalRequired = FUNCTIONAL_FEES_PER_YEAR * 0.5m;
            bool canEnroll = (newTuitionPaid >= minimumTuitionRequired) && (newFunctionalPaid >= minimumFunctionalRequired);

            return new PaymentFinalizationResult
            {
                PaymentId = payment.PaymentId,
                TransactionReference = payment.TransactionReference,
                Amount = payment.Amount,
                PaymentCategory = payment.PaymentCategory,
                StudentRegNumber = payment.RegNumber,
                StudentName = $"{student.FirstName} {student.LastName}",
                ReceiptPath = receiptPath,
                ReceiptGenerated = !string.IsNullOrEmpty(receiptPath),
                ReceiptEmailSent = receiptEmailSent,
                TuitionRequired = tuitionRequired,
                TuitionPaid = newTuitionPaid,
                TuitionRemaining = Math.Max(0, tuitionRequired - newTuitionPaid),
                FunctionalRequired = functionalRequired,
                FunctionalPaid = newFunctionalPaid,
                FunctionalRemaining = Math.Max(0, functionalRequired - newFunctionalPaid),
                TotalRequired = tuitionRequired + functionalRequired,
                TotalPaid = newCurrentPeriodPaid,
                TotalRemaining = Math.Max(0, (tuitionRequired + functionalRequired) - newCurrentPeriodPaid),
                CanEnroll = canEnroll,
                EnrollmentStatus = canEnroll ? "You can now enroll for courses" : "You still need to pay more to be eligible for enrollment",
                Status = payment.Status
            };
        }

        private async Task SaveDocument(string regNumber, string type, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var docType = await _context.DocumentTypes.FirstOrDefaultAsync(d => d.DocumentTypeName == type);
            if (docType == null) return;

            bool exists = await _context.Documents.AnyAsync(d =>
                d.RegNumber == regNumber && d.DocumentTypeId == docType.DocumentTypeId && d.DocumentPath == path);

            if (!exists)
            {
                _context.Documents.Add(new Document
                {
                    RegNumber = regNumber,
                    DocumentTypeId = docType.DocumentTypeId,
                    DocumentType = type,
                    DocumentPath = path,
                    Status = "GENERATED",
                    DateGenerated = DateTime.Now,
                    FeePaid = true,
                    QrVerificationCode = Guid.NewGuid().ToString(),
                    ExpiryDate = DateTime.Now.AddMonths(6),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
        }
    }
}