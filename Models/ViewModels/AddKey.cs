namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class AddKey
    {
        public string? KeyId { get; set; }
        public string? Building { get; set; }
        public string? RoomNumber { get; set; }
        public string? FloorNumber { get; set; }
        public int NumberOfKeys { get; set; } = 1;
        public List<string> TagNumbers { get; set; } = new();
    }
}
