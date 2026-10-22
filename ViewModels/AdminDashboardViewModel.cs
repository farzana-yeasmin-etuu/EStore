namespace EStore.ViewModels
{
    public class AdminDashboardViewModel
    {
        public string TodayLabel { get; set; } = "";

        // Sales = all non-cancelled orders
        public decimal TodaySales { get; set; }
        public int TodayOrders { get; set; }

        public decimal MonthSales { get; set; }
        public int MonthOrders { get; set; }

        // Null when last month had no sales (nothing to compare with)
        public decimal? MonthGrowthPercent { get; set; }

        // Revenue = orders that are really paid
        public decimal RevenueCollected { get; set; }
        public decimal AwaitingPayment { get; set; }
        public decimal AvgOrderValue { get; set; }

        public int PendingOrders { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }

        public int ActiveProducts { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // 30-day chart
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> ChartSales { get; set; } = new();
        public List<int> ChartOrders { get; set; } = new();

        public Dictionary<string, int> StatusCounts { get; set; } = new();

        public List<TopProductRow> TopProducts { get; set; } = new();
        public List<RecentOrderRow> RecentOrders { get; set; } = new();
        public List<LowStockRow> LowStockProducts { get; set; } = new();
    }

    public class TopProductRow
    {
        public string Name { get; set; } = "";
        public int Qty { get; set; }
        public decimal Revenue { get; set; }
    }

    public class RecentOrderRow
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "";
        public string OrderStatus { get; set; } = "";
        public string Date { get; set; } = "";
    }

    public class LowStockRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Stock { get; set; }
        public string ImageUrl { get; set; } = "";
    }
}