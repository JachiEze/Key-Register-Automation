namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class KeyLookup
    {
        public string Search { get; set; } = "";
        public List<KeyLookupViewModel> Results { get; set; } = new();
    }

    public class KeyLookupViewModel
    {
        public string? RequesterIGG { get; set; }
        public string? RequesterName { get; set; }
        public string? FloorNumber { get; set; }
        public string? AssignmentType { get; set; }
        public string? RoomNumber { get; set; }
        public string? TagNumber { get; set; }
        public string DisplayStatus { get; set; } = "OK";
    }
}
