using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace Functions.Models
{
    public class Order : ITableEntity
    {
        public string? PartitionKey { get; set; } = "Order";

        public string? RowKey { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [Required(ErrorMessage = "Customer ID is required")]
        public string? CustomerRowKey { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Product ID is required")]
        public string? ProductRowKey { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Product name cannot be longer than 100 characters")]
        public string? ProductName { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int? Quantity { get; set; }

        public double? UnitPrice { get; set; }

        public double? TotalPrice { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    }
}
