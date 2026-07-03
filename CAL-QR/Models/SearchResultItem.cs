namespace CAL_QR.Models
{
    public class SearchResultItem
    {
        public int Id { get; set; }
        public SearchEntityType EntityType { get; set; }
        public string DisplayTitle { get; set; } = string.Empty;
        public string DisplaySubtitle { get; set; } = string.Empty;
    }
}
