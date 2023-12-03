
namespace BetsParserLib
{
    public class BookmakerGameKoeff
    {
        public string BookmakerName { get; set; }
        public double Total { get; set; }
        public List<HistoryRow> Over { get; set; } = new List<HistoryRow>();
        public List<HistoryRow> Under { get; set; } = new List<HistoryRow>();
    }
}
