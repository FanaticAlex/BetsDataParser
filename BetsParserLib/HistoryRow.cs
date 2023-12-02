
namespace BetsParserLib
{
    public record HistoryRow
    {
        public DateTime? Date { get; set; }
        public double Value { get; set; }
        public double Difference { get; set; }
    }
}
