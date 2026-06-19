namespace KEYREGISTERAUTOMATION.Models
{
    public class EmailSettings
    {
        public int Id { get; set; }
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; }
        public string Address { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
