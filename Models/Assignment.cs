namespace KEYREGISTERAUTOMATION.Models
{
    public class AssignmentRecord
    {
        public int Id { get; set; }
        public string RequesterIGG { get; set; }
        public string RequesterName { get; set; }
        public string Department { get; set; }
        public string Division { get; set; }
        public string CollectorType { get; set; }
        public string? DelegateName { get; set; }
        public string AssignmentType { get; set; }
        public string Status { get; set; } = "Issued";
        public string KeyId { get; set; }
        public DateTime? DateRequested { get; set; } = DateTime.Now;
        public int? Duration { get; set; }
        public DateTime? DueDate { get; set; }
    }
}