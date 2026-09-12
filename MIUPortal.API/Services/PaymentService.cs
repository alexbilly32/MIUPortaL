using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public interface IPaymentService
    {
        Task<decimal> GetStudentFeesBalanceAsync(string regNumber);
        Task<decimal> GetTotalPaidAsync(string regNumber);
        Task<List<Payment>> GetPaymentHistoryAsync(string regNumber);
        Task<bool> ProcessPaymentAsync(string regNumber, decimal amount, string paymentMethod);
    }

    public class PaymentService : IPaymentService
    {
        private readonly MIUContext _context;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(MIUContext context, ILogger<PaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<decimal> GetStudentFeesBalanceAsync(string regNumber)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                return student?.FeesBalance ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fees balance");
                return 0;
            }
        }

        public async Task<decimal> GetTotalPaidAsync(string regNumber)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                return student?.TotalPaid ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total paid");
                return 0;
            }
        }

        public async Task<List<Payment>> GetPaymentHistoryAsync(string regNumber)
        {
            try
            {
                return await _context.Payments
                    .Where(p => p.RegNumber == regNumber)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment history");
                return new List<Payment>();
            }
        }

        public async Task<bool> ProcessPaymentAsync(string regNumber, decimal amount, string paymentMethod)
        {
            try
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.RegNumber == regNumber);

                if (student == null)
                    return false;

                var payment = new Payment
                {
                    RegNumber = regNumber,
                    Amount = amount,
                    PaymentMethod = paymentMethod,
                    Status = "Confirmed",
                    PaymentDate = DateTime.Now,
                    TransactionReference = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.Now
                };

                student.TotalPaid += amount;
                student.FeesBalance -= amount;
                student.UpdatedAt = DateTime.Now;

                _context.Payments.Add(payment);
                _context.Students.Update(student);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment");
                return false;
            }
        }
    }
}

