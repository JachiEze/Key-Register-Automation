using System.ComponentModel.DataAnnotations;

namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class UpdateKeyExistingTag
    {
        public int Id { get; set; }

        public string TagNumber { get; set; } = "";

        public string Status { get; set; } = "Free";
    }

    public class UpdateKey
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string KeyId { get; set; } = "";

        [Required]
        public string Building { get; set; } = "";

        [Required]
        public string FloorNumber { get; set; } = "";

        [Required]
        public string RoomNumber { get; set; } = "";

        public int NumberOfNewKeys { get; set; } = 0;

        public List<UpdateKeyExistingTag> ExistingKeys { get; set; } = new();

        public List<string> NewTagNumbers { get; set; } = new();
    }
}
