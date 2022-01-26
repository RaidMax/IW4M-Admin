using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Helpers
{
    public class Report
    {
        public EFClient Target { get; set; }
        public EFClient Origin { get; set; }
        public string Reason { get; set; }
    }
}