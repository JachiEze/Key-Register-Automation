using KEYREGISTERAUTOMATION.Models;

namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class AllAssignmentGrid
    {
        public string? Search { get; set; }
        public List<AssignmentRecord> Assignments { get; set; } = new List<AssignmentRecord>();

    }
}
