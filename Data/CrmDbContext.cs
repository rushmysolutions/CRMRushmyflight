using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Entities;

namespace RushMyBookings.Crm.Data;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<BookFlightDetail> Bookings => Set<BookFlightDetail>();
    public DbSet<PassengerList> Passengers => Set<PassengerList>();
    public DbSet<CrmUser> Users => Set<CrmUser>();
    public DbSet<BookingType> BookingTypes => Set<BookingType>();
    public DbSet<BookComment> Comments => Set<BookComment>();
    public DbSet<BookAttachment> Attachments => Set<BookAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookFlightDetail>(entity =>
        {
            entity.ToTable("tbl_book_flightdetail");
            entity.HasKey(e => e.BookId);
            entity.Property(e => e.BookId).HasColumnName("bookid");
            entity.Property(e => e.ReferenceNo).HasColumnName("booking_eference_no");
            entity.Property(e => e.BookingType).HasColumnName("booking_type");
            entity.Property(e => e.CustomerName).HasColumnName("nbname");
            entity.Property(e => e.Email).HasColumnName("bemail");
            entity.Property(e => e.Phone).HasColumnName("bphone");
            entity.Property(e => e.Airline).HasColumnName("airlinename");
            entity.Property(e => e.Pnr).HasColumnName("tairlineconfirmation");
            entity.Property(e => e.TicketStatus).HasColumnName("tticketstatus");
            entity.Property(e => e.TotalAmount).HasColumnName("atotal");
            entity.Property(e => e.Currency).HasColumnName("bookingcurrency");
            entity.Property(e => e.CreatedDate).HasColumnName("date");
            entity.Property(e => e.TravelDate).HasColumnName("travellingdate");
            entity.Property(e => e.AssignedUser).HasColumnName("assigneduser");
            entity.Property(e => e.FlightInfo).HasColumnName("flight_info");
            entity.Property(e => e.Supplier).HasColumnName("tsupplier");
            entity.Property(e => e.TicketCost).HasColumnName("aticketcost");
            entity.Property(e => e.McoAmount).HasColumnName("amco");
            entity.Property(e => e.PaymentMethod).HasColumnName("paymentmethod");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.QualityStatus).HasColumnName("qualitystatus");
            entity.Property(e => e.IsHide).HasColumnName("ishide");
            entity.Property(e => e.FlightPnr).HasColumnName("flight_pnr");
            entity.Property(e => e.ReturnDate).HasColumnName("retruningdate");
            entity.Property(e => e.NoOfPeople).HasColumnName("noofpeople");
            entity.Property(e => e.CardHolderName).HasColumnName("cardholdername");
            entity.Property(e => e.CardNumber).HasColumnName("ccnumber");
            entity.Property(e => e.LastFourCardDigits).HasColumnName("lastfourdigitcc");
            entity.Property(e => e.ExpiryDate).HasColumnName("expirydate");
            entity.Property(e => e.Cvv).HasColumnName("cvnnumber");
            entity.Property(e => e.InboundPhone).HasColumnName("cphone");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Zip).HasColumnName("zip");
            entity.Property(e => e.ClassType).HasColumnName("classtype");
            entity.Property(e => e.McoSupplier).HasColumnName("amcosupplier");
            entity.Property(e => e.McoStatus).HasColumnName("amcostatus");
            entity.Property(e => e.PartialRefund).HasColumnName("partialrefund");
            entity.Property(e => e.NetMco).HasColumnName("anetmco");
            entity.Property(e => e.SplitCharges).HasColumnName("splitcharges");
            entity.Property(e => e.PaymentUrl).HasColumnName("paymentURL");
            entity.Property(e => e.PaymentMode).HasColumnName("paymentmod");
            entity.Property(e => e.CustomEmailSubject).HasColumnName("customemailsubject");
            entity.Property(e => e.BookingLang).HasColumnName("bookingLang");
            entity.Property(e => e.CreatedBy).HasColumnName("createdby");
            entity.Property(e => e.TravelPhoneNumber).HasColumnName("tavelphonenumber");
            entity.Property(e => e.CustomerAckStatus).HasColumnName("customer_ack_status");
            entity.Property(e => e.CustomerAckIp).HasColumnName("customer_ack_ip");
            entity.Property(e => e.CustomerAckLocation).HasColumnName("customer_ack_location");
            entity.Property(e => e.CustomerAckDate).HasColumnName("customer_ack_date");
            entity.Property(e => e.FollowUpBy).HasColumnName("followupby");
            entity.Property(e => e.FollowUpDate).HasColumnName("followupdate");

            // bookingid in tbl_book_coments is a legacy varchar key, not an EF FK to bookid.
            entity.Ignore(e => e.Comments);
        });

        modelBuilder.Entity<PassengerList>(entity =>
        {
            entity.ToTable("tbl_passenger_list");
            entity.HasKey(e => e.PassId);
            entity.Property(e => e.PassId).HasColumnName("pass_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.FirstName).HasColumnName("firstname");
            entity.Property(e => e.MiddleName).HasColumnName("middlename");
            entity.Property(e => e.LastName).HasColumnName("lastname");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Gender).HasColumnName("gender");
            entity.Property(e => e.TicketNo).HasColumnName("ticketno");

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Passengers)
                .HasForeignKey(e => e.BookingId)
                .HasPrincipalKey(b => b.BookId);
        });

        modelBuilder.Entity<CrmUser>(entity =>
        {
            entity.ToTable("tbl_users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FirstName).HasColumnName("user_fname");
            entity.Property(e => e.LastName).HasColumnName("user_lname");
            entity.Property(e => e.Email).HasColumnName("user_email");
            entity.Property(e => e.PasswordHash).HasColumnName("user_password");
            entity.Property(e => e.Status).HasColumnName("user_status");
            entity.Property(e => e.UserType).HasColumnName("user_type");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.LastLogin).HasColumnName("user_last_login");
        });

        modelBuilder.Entity<BookingType>(entity =>
        {
            entity.ToTable("tbl_booking_type");
            entity.HasKey(e => e.TypeId);
            entity.Property(e => e.TypeId).HasColumnName("type_id");
            entity.Property(e => e.TypeName).HasColumnName("typename");
            entity.Property(e => e.Status).HasColumnName("bstatus");
            entity.Property(e => e.Border).HasColumnName("border");
        });

        modelBuilder.Entity<BookComment>(entity =>
        {
            entity.ToTable("tbl_book_coments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("cid")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.BookingId)
                .HasColumnName("bookingid")
                .HasMaxLength(250)
                .IsRequired();
            entity.Property(e => e.Username)
                .HasColumnName("username")
                .HasMaxLength(100);
            entity.Property(e => e.Comment).HasColumnName("commentarea");
            entity.Property(e => e.CreatedDate)
                .HasColumnName("createddate")
                .HasMaxLength(200);
        });

        modelBuilder.Entity<BookAttachment>(entity =>
        {
            entity.ToTable("tbl_book_attachment");
            entity.HasKey(e => e.AttachId);
            entity.Property(e => e.AttachId).HasColumnName("attach_id");
            entity.Property(e => e.BookingId).HasColumnName("attach_booking_id");
            entity.Property(e => e.AttachFiles).HasColumnName("attach_files");
            entity.Property(e => e.InitialUploadQuality).HasColumnName("initial_upload_quality");

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.Attachments)
                .HasForeignKey(e => e.BookingId)
                .HasPrincipalKey(b => b.BookId);
        });
    }
}
