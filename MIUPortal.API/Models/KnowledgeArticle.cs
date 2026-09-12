using System;

namespace MIUPortal.API.Models
{
    /// <summary>
    /// A single chunk of MIU knowledge (either curated FAQ, portal-derived fact,
    /// or synced content from the public university website) that the chatbot
    /// can retrieve and cite in its answers.
    /// </summary>
    public class KnowledgeArticle
    {
        public int Id { get; set; }

        // "FAQ", "Website", "Portal" - lets you tell where a chunk came from
        public string Source { get; set; } = "FAQ";

        // Free-form grouping, e.g. "General", "Fees", "Contact", "Campus"
        public string Category { get; set; } = "General";

        public string Title { get; set; } = "";

        // Plain-text content only. Strip HTML before saving - never store
        // raw markup, scripts, or comments (which can contain build/tech info).
        public string Content { get; set; } = "";

        // Simple keyword tags used for retrieval matching (comma separated,
        // lowercase). E.g. "fees,tuition,payment,bursar"
        public string Tags { get; set; } = "";

        // Higher = more important. Used as a retrieval boost - a highly
        // relevant but low-priority article shouldn't necessarily lose to a
        // barely-relevant high-priority one, so this multiplies the keyword
        // score rather than overriding it entirely.
        public int Priority { get; set; } = 1;

        // Original public URL if this came from the university website.
        // Never store internal/admin/portal URLs here.
        public string? SourceUrl { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}