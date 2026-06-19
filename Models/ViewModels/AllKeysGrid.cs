using System.Collections.Generic;
using KEYREGISTERAUTOMATION.Models;

namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class AllKeysGrid
    {
        public string? Search { get; set; }
        public List<Keys> Keys { get; set; } = new List<Keys>();
        public List<IndividualKey> IndividualKeys { get; set; } = new List<IndividualKey>();
    }
}

