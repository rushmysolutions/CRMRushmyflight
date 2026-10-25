using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.ViewModels
{
    public class AgentViewModels
    {
    }
    public sealed class AgentListItem
    {
        public int UserId { get; init; }
        public string? Username { get; init; }
        public string? DialerId { get; init; }
        public string? Password { get; init; }
        public string Name { get; init; } = "";
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? UserType { get; init; }
        public string CreatedDate { get; init; } = "";
        public bool IsActive { get; init; }
    }

    public sealed class AgentListViewModel
    {
        public IReadOnlyList<AgentListItem> Agents { get; init; } = [];
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;
        public int Total { get; init; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }

    public sealed class AgentFormViewModel
    {
        public static readonly string[] UserTypes = { "Agent", "Admin" };
        public static readonly Dictionary<string, string> Countries = new()
        {
            ["US"] = "United States",
            ["CA"] = "Canada"
        };

        public int UserId { get; set; }
        public bool IsEdit => UserId > 0;

        [Required(ErrorMessage = "First Name required hai"), StringLength(60)]
        public string FirstName { get; set; } = "";

        [StringLength(60)] public string? LastName { get; set; }
        [StringLength(15)] public string? TollFreeNo { get; set; }
        [StringLength(15)] public string? DirectLine { get; set; }
        [StringLength(5)] public string? Extn { get; set; }

        [Required(ErrorMessage = "Phone Number required hai"), StringLength(15)]
        public string Phone { get; set; } = "";

        [EmailAddress(ErrorMessage = "Valid email daaliye"), StringLength(60)]
        public string? Email { get; set; }

        [RegularExpression(@"^(0[1-9]|1[0-2])/(0[1-9]|[12]\d|3[01])/\d{4}$",
            ErrorMessage = "DOB format MM/DD/YYYY hona chahiye")]
        public string? Dob { get; set; }

        public short Sex { get; set; }   // 0 = Male, 1 = Female

        [StringLength(60)] public string? Address { get; set; }
        [StringLength(60)] public string? City { get; set; }
        [StringLength(60)] public string? State { get; set; }
        [StringLength(8)] public string? ZipCode { get; set; }

        [Required] public string Country { get; set; } = "US";

        [Required(ErrorMessage = "Type select kariye")]
        public string UserType { get; set; } = "Agent";

        [Required(ErrorMessage = "Username required hai"), StringLength(60)]
        public string Username { get; set; } = "";

        [StringLength(60)] public string? DialerId { get; set; }
    }
}
