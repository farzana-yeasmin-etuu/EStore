namespace EStore.ViewModels
{
    public class PageVisitDashboardViewModel
    {
        public int TotalVisits { get; set; }

        public int UniqueVisitors { get; set; }

        public int LoggedInVisits { get; set; }

        public int GuestVisits { get; set; }

        public List<PopularPageViewModel> PopularPages { get; set; }
            = new();

        public List<RecentPageVisitViewModel> RecentVisits { get; set; }
            = new();
    }

    public class PopularPageViewModel
    {
        public string PageName { get; set; } = "";

        public int Visits { get; set; }
    }

    public class RecentPageVisitViewModel
    {
        public string PageName { get; set; } = "";

        public string Url { get; set; } = "";

        public string? UserId { get; set; }

        public string? VisitorId { get; set; }

        public DateTime VisitedAt { get; set; }

        public bool IsLoggedIn { get; set; }
    }
}