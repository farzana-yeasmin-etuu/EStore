namespace EStore.ViewModels
{
    // DataTables sends these values on every page change, search and sort
    public class DataTablesRequest
    {
        public int Draw { get; set; }

        // How many rows to skip
        public int Start { get; set; }

        // Rows per page
        public int Length { get; set; } = 10;

        public DtSearch Search { get; set; } = new();

        public List<DtOrder> Order { get; set; } = new();
    }

    public class DtSearch
    {
        public string? Value { get; set; }
    }

    public class DtOrder
    {
        // Which column to sort by
        public int Column { get; set; }

        // "asc" or "desc"
        public string Dir { get; set; } = "asc";
    }
}