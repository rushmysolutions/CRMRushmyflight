using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities
{
    public class Tfn
    {
        // DB code => Display label  (codes apne data se confirm kar lena)
        public static readonly Dictionary<string, string> Types = new()
        {
            ["c"] = "Customer",
            ["b"] = "Buffer",
            ["n"] = "Non Buffer"
        };

        public int TfnId { get; set; }

        [Required(ErrorMessage = "TFN Number required hai")]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "TFN exactly 4 digits ka hona chahiye")]
        public string TfnNumber { get; set; } = "";

        [Required(ErrorMessage = "DID Number required hai")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "DID exactly 11 digits ka hona chahiye")]
        public string DidNumber { get; set; } = "";

        [Required(ErrorMessage = "Type select kariye")]
        public string Type { get; set; } = "";

        public bool IsActive { get; set; } = true;
    }
}
