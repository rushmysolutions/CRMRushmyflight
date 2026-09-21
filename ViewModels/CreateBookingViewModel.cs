using System.ComponentModel.DataAnnotations;
using RushMyBookings.Crm.Models;

namespace RushMyBookings.Crm.ViewModels;

public sealed class CreateBookingViewModel
{
    public int? BookId { get; set; }

    public bool IsEdit => BookId is > 0;

    [Required(ErrorMessage = "Booking type is required.")]
    [Display(Name = "Booking Type")]
    public string? BookingType { get; set; }

    [Display(Name = "Booking Reference No")]
    public string? ReferenceNo { get; set; }

    [Display(Name = "Language")]
    public int BookingLang { get; set; }

    [Display(Name = "Custom Email Subject")]
    public string? CustomEmailSubject { get; set; }

    [Display(Name = "PNR")]
    public string? FlightPnr { get; set; }

    [Display(Name = "Traveling Date")]
    public string? TravelDate { get; set; }

    [Display(Name = "Returning Date")]
    public string? ReturnDate { get; set; }

    [Display(Name = "TFN")]
    public string? Tfn { get; set; }

    public List<PassengerFormModel> Passengers { get; set; } = [new()];

    [Display(Name = "Card Holder Name")]
    public string? CardHolderName { get; set; }

    [Display(Name = "Card Type")]
    public string? CardType { get; set; }

    [Display(Name = "Credit Card No")]
    public string? CardNumber { get; set; }

    [Display(Name = "Expiration Date")]
    public string? ExpiryDate { get; set; }

    [Display(Name = "CVV")]
    public string? Cvv { get; set; }

    [Display(Name = "Inbound No.")]
    public string? InboundPhone { get; set; }

    [Display(Name = "Billing No.")]
    public string? BillingPhone { get; set; }

    [Display(Name = "Email ID")]
    public string? Email { get; set; }

    [Display(Name = "Billing Address")]
    public string? BillingAddress { get; set; }

    [Display(Name = "Zip Code")]
    public string? ZipCode { get; set; }

    [Display(Name = "Airline Confirmation")]
    public string? AirlineConfirmation { get; set; }

    [Display(Name = "Ticket Status")]
    public string TicketStatus { get; set; } = "0";

    [Display(Name = "Class of Booking")]
    public string? ClassType { get; set; }

    [Display(Name = "Supplier")]
    public string? Supplier { get; set; }

    [Display(Name = "Ticket Cost")]
    public string? TicketCost { get; set; }

    [Display(Name = "Airline Name")]
    public string? AirlineName { get; set; }

    [Display(Name = "MCO")]
    public string? McoAmount { get; set; }

    [Display(Name = "Total")]
    public string? TotalAmount { get; set; }

    [Display(Name = "Split Charges")]
    public string SplitCharges { get; set; } = "no";

    [Display(Name = "MCO Supplier & Mail From")]
    public string? McoSupplier { get; set; }

    [Display(Name = "MCO Status")]
    public string McoStatus { get; set; } = "0";

    [Display(Name = "Partial Refund")]
    public string? PartialRefund { get; set; }

    [Display(Name = "Net MCO")]
    public string? NetMco { get; set; }

    [Display(Name = "Currency")]
    public string Currency { get; set; } = "USD";

    [Display(Name = "Payment URL")]
    public string? PaymentUrl { get; set; }

    [Display(Name = "Payment Mode")]
    public string PaymentMode { get; set; } = "manual";

    [Display(Name = "Comments")]
    public string? Comment { get; set; }

    public IReadOnlyList<BookingTypeOption> BookingTypes { get; set; } = [];
}

public sealed class PassengerFormModel
{
    public int PassId { get; set; }

    [Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [Display(Name = "Gender")]
    public string? Gender { get; set; }

    [Display(Name = "Day")]
    public int? DobDay { get; set; }

    [Display(Name = "Month")]
    public int? DobMonth { get; set; }

    [Display(Name = "Year")]
    public int? DobYear { get; set; }

    [Display(Name = "Ticket No.")]
    public string? TicketNo { get; set; }

    public string? FormattedDob =>
        DobDay is > 0 && DobMonth is > 0 && DobYear is > 0
            ? $"{DobMonth}/{DobDay}/{DobYear}"
            : null;
}
