namespace Cafe.Models.ViewModels
{
    /// <summary>View-layer models for the shared UI partials (Views/Shared/_*.cshtml).
    /// Pure presentation — no domain meaning, no controller contracts.</summary>
    public class KpiCardVm
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Icon { get; set; } = "fa-chart-line";

        /// <summary>Visual tint: default | accent | success | warning | danger | info.</summary>
        public string Tint { get; set; } = "default";

        /// <summary>Optional chip text, e.g. "+8.2%" or "Operating".</summary>
        public string? TrendText { get; set; }

        /// <summary>up | down | neutral — colors the trend chip.</summary>
        public string? TrendDirection { get; set; }

        /// <summary>Animate the value on load (exact server string is restored).</summary>
        public bool Countup { get; set; } = true;

        /// <summary>When set, the whole card becomes a link.</summary>
        public string? Href { get; set; }
    }

    public class PageHeaderVm
    {
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? Icon { get; set; }
        public string? ActionText { get; set; }
        public string? ActionHref { get; set; }
        public string? ActionIcon { get; set; }
        /// <summary>Optional back-link (renders a ghost arrow button before the title).</summary>
        public string? BackHref { get; set; }
    }

    public class EmptyStateVm
    {
        public string Icon { get; set; } = "fa-inbox";
        public string Title { get; set; } = "Nothing here yet";
        public string? Message { get; set; }
        public string? ActionText { get; set; }
        public string? ActionHref { get; set; }
    }

    public class PaginationVm
    {
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        /// <summary>Query-string parameter name that carries the page number.</summary>
        public string PageParam { get; set; } = "page";
    }
}
