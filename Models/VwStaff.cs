using System.ComponentModel.DataAnnotations;

namespace KEYREGISTERAUTOMATION.Models
{
    public class VwStaff
    {
        [Key]
        public string? IGG { get; set; }
        public string? Name { get; set; }
        public string? PositionNumber { get; set; }
        public string? Descr_location { get; set; }
        public string? LineManager { get; set; }
        public string? Manager { get; set; }
        public string? Empl_Class { get; set; }
        public string? Location { get; set; }
        public string? DivisionID { get; set; }
        public string? Division { get; set; }
        public string? DepartmentID { get; set; }
        public string? Department { get; set; }
        public string? Directorate { get; set; }
        public string? Email { get; set; }
        public string? Search { get; set; }
        public string? Locationdistrict { get; set; }
    }
}
