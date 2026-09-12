using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MIUPortal.API.Models
{
    [Table("PaymentGateways")]
    public class PaymentGateway
    {
        [Key]
        public int GatewayId { get; set; }
        public string? GatewayName { get; set; }
        public string? ProviderName { get; set; }
        public string? ApiKey { get; set; }
        public string? MerchantId { get; set; }
        public bool IsActive { get; set; }
        public string? Currency { get; set; }
        public int? MinAmount { get; set; }
        public int? MaxAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
