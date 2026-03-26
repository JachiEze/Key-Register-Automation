using System.Collections.Generic;
using KEYREGISTERAUTOMATION.Models;

namespace KEYREGISTERAUTOMATION.Models.ViewModels
{
    public class AllKeysGrid
    {
        public string? Search { get; set; }
        public List<AllKeyTags> Keys { get; set; } = new List<AllKeyTags>();
    }
}

