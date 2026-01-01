namespace Traveler.Core.Models
{
    public class VendorItem
    {
        public string VendorName { get; set; } = string.Empty;
        public string VendorIcon { get; set; } = string.Empty;
        public string SaleItem { get; set; } = string.Empty;
        public string SaleItemIcon { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g. "Selling High Stat Armor"
    }
}
