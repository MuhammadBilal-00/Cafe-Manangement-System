using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 6 CRM: a sales lead.</summary>
    public class Lead : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(120)] public string Name { get; set; } = string.Empty;
        [StringLength(120)] public string? Email { get; set; }
        [StringLength(30)] public string? Phone { get; set; }
        [StringLength(60)] public string? Source { get; set; }
        /// <summary>New | Contacted | Qualified | Won | Lost</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "New";
        [StringLength(400)] public string? Notes { get; set; }
        public int? AssignedToId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    }

    public class FollowUp : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required] public int LeadId { get; set; }
        public DateTime DueAt { get; set; } = DateTime.Now.AddDays(1);
        [StringLength(300)] public string? Note { get; set; }
        public bool Done { get; set; }

        [ForeignKey("LeadId")] public Lead? Lead { get; set; }
    }

    /// <summary>Phase 6 CRM: a marketing campaign (email/SMS blast to a segment).</summary>
    public class Campaign : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required][StringLength(120)] public string Name { get; set; } = string.Empty;
        /// <summary>Email | SMS</summary>
        [Required][StringLength(20)] public string Channel { get; set; } = "Email";
        /// <summary>AllCustomers | Leads</summary>
        [StringLength(30)] public string Segment { get; set; } = "AllCustomers";
        [StringLength(200)] public string? Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        /// <summary>Draft | Sent</summary>
        [Required][StringLength(20)] public string Status { get; set; } = "Draft";
        public int Recipients { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? SentAt { get; set; }
    }
}
