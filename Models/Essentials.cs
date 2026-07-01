using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cafe.Models
{
    /// <summary>Phase 8 (51): a shared document/file reference.</summary>
    public class Document : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(150)] public string Title { get; set; } = string.Empty;
        [StringLength(40)] public string? Category { get; set; }
        [StringLength(400)] public string? FileUrl { get; set; }
        [StringLength(400)] public string? Notes { get; set; }
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Phase 8 (52): a company memo / notice.</summary>
    public class Memo : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(150)] public string Title { get; set; } = string.Empty;
        [Required] public string Body { get; set; } = string.Empty;
        public bool Pinned { get; set; }
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Phase 8 (53): a reminder; creating one raises a notification.</summary>
    public class Reminder : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(150)] public string Title { get; set; } = string.Empty;
        public DateTime DueAt { get; set; } = DateTime.Now.AddDays(1);
        public bool Done { get; set; }
        public int? OwnerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Phase 8 (54): an internal direct message between two users.</summary>
    public class Message : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required] public int FromUserId { get; set; }
        [Required] public int ToUserId { get; set; }
        [Required][StringLength(2000)] public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("FromUserId")] public User? FromUser { get; set; }
        [ForeignKey("ToUserId")] public User? ToUser { get; set; }
    }

    /// <summary>Phase 8 (55): a knowledge-base article / SOP.</summary>
    public class KnowledgeBaseArticle : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [Required][StringLength(180)] public string Title { get; set; } = string.Empty;
        [StringLength(60)] public string? Category { get; set; }
        [Required] public string Body { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = true;
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
