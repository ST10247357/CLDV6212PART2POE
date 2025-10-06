using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace FunctionApp2.Models
{
    public class Customer : ITableEntity
    {
        public string? PartitionKey { get; set; } = "Customer";

        public string? RowKey { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(50, ErrorMessage = "Customer name cannot be longer than 50 characters")]
        public string? Customer_Name { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please enter a valid phone number")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "Address is required")]
        public string? Address { get; set; }
        public string? FileName { get; set; }

    }
}
