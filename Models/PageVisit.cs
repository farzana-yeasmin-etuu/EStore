namespace EStore.Models
{
    public class PageVisit
    {
        public int Id { get; set; }

        // Logged-in customer's Identity User ID
        public string? UserId { get; set; }

        // Used for visitors who are not logged in
        public string? VisitorId { get; set; }

        // Page name, e.g. Home, Kurti, Men's Collection
        public string PageName { get; set; } = "";

        // Actual URL visited
        public string Url { get; set; } = "";

        // When the page was visited
        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    }
}