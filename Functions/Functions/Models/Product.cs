using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace Functions.Models
{
    public class Product : ITableEntity
    {
        public string? PartitionKey { get; set; } = "Product";

        public string? RowKey { get; set; } 

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Name cannot be longer than 100 characters")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public double Price { get; set; }

        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public int? Quantity { get; set; }
    }
}
