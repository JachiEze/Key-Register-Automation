using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KEYREGISTERAUTOMATION.Models
{
    public class IndividualKey
    {
        public int Id { get; set; }
        [ForeignKey(nameof(ParentKey))]
        public int ParentKeyId { get; set; }
        [Required]
        public string? TagNumber { get; set; }
        [Required]
        public string Status { get; set; } = "Free";
        public Keys ParentKey { get; set; } = null!;
    }
}
