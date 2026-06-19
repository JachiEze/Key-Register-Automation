using System.ComponentModel.DataAnnotations.Schema;

namespace KEYREGISTERAUTOMATION.Models
{
    public class Keys
    {
        public const string DefaultKeyCode = "TF441910";
        public int Id { get; set; }
        public string? KeyId { get; set; }
        public string? KeyCode { get; set; } = DefaultKeyCode;
        public string? Building { get; set; }
        public string? RoomNumber { get; set; }
        public string? FloorNumber { get; set; }
        public int TotalNoofKeys { get; set; }
        public int NoofKeysAvaialble { get; set; }
        public ICollection<IndividualKey> IndividualKeys { get; set; } = new List<IndividualKey>();
    }

}

