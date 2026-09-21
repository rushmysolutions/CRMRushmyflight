using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;

namespace RushMyBookings.Crm.Services;

public sealed class CrmDataService(CrmDbContext db, IMemoryCache cache) : ICrmDataService
{
    private const string DbConnectedCacheKey = "crm:db-connected";
    private const string DashboardStatsCacheKey = "crm:dashboard-stats";
    private static readonly TimeSpan DbConnectedCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DashboardStatsCacheTtl = TimeSpan.FromMinutes(3);

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(DbConnectedCacheKey, out bool cached))
        {
            return cached;
        }

        var connected = await ProbeDatabaseConnectionAsync(cancellationToken);
        cache.Set(
            DbConnectedCacheKey,
            connected,
            connected ? DbConnectedCacheTtl : TimeSpan.FromSeconds(15));

        return connected;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(DashboardStatsCacheKey, out DashboardStats? cached) && cached is not null)
        {
            return cached;
        }

        var stats = await LoadDashboardStatsAsync(cancellationToken);
        cache.Set(DashboardStatsCacheKey, stats, DashboardStatsCacheTtl);
        cache.Set(DbConnectedCacheKey, true, DbConnectedCacheTtl);
        return stats;
    }

    private async Task<bool> ProbeDatabaseConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task<DashboardStats> LoadDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var counts = await db.Database.SqlQuery<DashboardCountsRow>($"""
            SELECT
                (SELECT COUNT(*) FROM tbl_book_flightdetail) AS TotalBookings,
                (SELECT COUNT(*) FROM tbl_book_flightdetail WHERE ishide = 0) AS VisibleBookings,
                (SELECT COALESCE(SUM(atotal), 0) FROM tbl_book_flightdetail WHERE ishide = 0) AS TotalRevenue,
                (SELECT COUNT(*) FROM tbl_passenger_list WHERE booking_id > 0) AS TotalPassengers,
                (SELECT COUNT(*) FROM tbl_users WHERE user_status = '1') AS TotalAgents,
                (SELECT COUNT(*) FROM tbl_book_coments) AS TotalComments
            """)
            .FirstAsync(cancellationToken);

        var typeLookup = await db.BookingTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.TypeId.ToString(), t => t.TypeName ?? "Unknown", cancellationToken);

        var bookingsByType = await db.Bookings.AsNoTracking()
            .Where(b => b.IsHide == 0)
            .GroupBy(b => b.BookingType ?? "0")
            .Select(g => new { BookingType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync(cancellationToken);

        var byType = bookingsByType
            .Select(x => new BookingTypeCount
            {
                TypeName = typeLookup.TryGetValue(x.BookingType, out var name) ? name : "Unknown",
                Count = x.Count
            })
            .ToList();

        var byMonth = await db.Bookings.AsNoTracking()
            .Where(b => b.IsHide == 0 && b.CreatedDate != null && b.CreatedDate != "" && b.CreatedDate!.Length >= 7)
            .GroupBy(b => b.CreatedDate!.Substring(0, 7))
            .Select(g => new MonthlyBookingCount { Month = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Month)
            .Take(12)
            .OrderBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return new DashboardStats
        {
            TotalBookings = counts.TotalBookings,
            VisibleBookings = counts.VisibleBookings,
            TotalPassengers = counts.TotalPassengers,
            TotalAgents = counts.TotalAgents,
            TotalComments = counts.TotalComments,
            TotalRevenue = counts.TotalRevenue,
            BookingsByType = byType,
            BookingsByMonth = byMonth
        };
    }

    public async Task<PagedResult<BookingSummary>> GetBookingsAsync(
        int page,
        int pageSize,
        BookingSearchFilter filter,
        IReadOnlyDictionary<int, string>? typeLookup = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Bookings.AsNoTracking().Where(b => b.IsHide == 0);

        if (!string.IsNullOrWhiteSpace(filter.Phone))
        {
            var term = filter.Phone.Trim();
            query = query.Where(b =>
                (b.Phone != null && b.Phone.Contains(term)) ||
                (b.InboundPhone != null && b.InboundPhone.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            var term = filter.Email.Trim();
            query = query.Where(b => b.Email != null && b.Email.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var term = filter.Name.Trim();
            query = query.Where(b => b.CustomerName != null && b.CustomerName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.BookingNo))
        {
            var term = filter.BookingNo.Trim();
            query = query.Where(b => b.ReferenceNo != null && b.ReferenceNo.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.AirlineConfirmation))
        {
            var term = filter.AirlineConfirmation.Trim();
            query = query.Where(b => b.Pnr != null && b.Pnr.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.LastFourCc))
        {
            var term = filter.LastFourCc.Trim();
            query = query.Where(b => b.LastFourCardDigits != null && b.LastFourCardDigits.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.DateFrom))
        {
            var from = filter.DateFrom.Trim();
            query = query.Where(b => b.CreatedDate != null && string.Compare(b.CreatedDate, from) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.DateTo))
        {
            var to = filter.DateTo.Trim();
            query = query.Where(b => b.CreatedDate != null && string.Compare(b.CreatedDate, to) <= 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            query = query.Where(b => b.BookingType == filter.Type.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var bookings = await query
            .OrderByDescending(b => b.BookId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.BookId,
                b.ReferenceNo,
                b.BookingType,
                b.CustomerName,
                b.Email,
                b.Phone,
                b.Airline,
                b.Pnr,
                b.TicketStatus,
                b.TotalAmount,
                b.McoAmount,
                b.NetMco,
                b.Currency,
                b.CreatedDate,
                b.TravelDate,
                b.AssignedUser,
                b.CreatedBy,
                b.CustomerAckStatus,
                b.McoStatus,
                b.QualityStatus,
                b.FollowUpBy,
                b.FollowUpDate
            })
            .ToListAsync(cancellationToken);

        typeLookup ??= await db.BookingTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.TypeId, t => t.TypeName ?? "Unknown", cancellationToken);

        var userIds = bookings
            .SelectMany(b => new[] { b.AssignedUser, b.CreatedBy })
            .Where(id => !string.IsNullOrWhiteSpace(id) && int.TryParse(id, out _))
            .Select(id => int.Parse(id!))
            .Distinct()
            .ToList();

        var userLookup = userIds.Count == 0
            ? new Dictionary<int, string?>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Username, cancellationToken);

        var bookIds = bookings.Select(b => b.BookId).ToList();
        var bookIdStrings = bookIds.Select(id => id.ToString()).ToList();
        var commentCounts = bookIds.Count == 0
            ? new Dictionary<string, int>()
            : await db.Comments.AsNoTracking()
                .Where(c => bookIdStrings.Contains(c.BookingId))
                .GroupBy(c => c.BookingId)
                .Select(g => new { BookingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BookingId, x => x.Count, cancellationToken);

        var attachmentRows = bookIds.Count == 0
            ? []
            : await db.Attachments.AsNoTracking()
                .Where(a => bookIds.Contains(a.BookingId))
                .Select(a => new { a.BookingId, a.AttachFiles })
                .ToListAsync(cancellationToken);

        var attachmentCounts = attachmentRows
            .GroupBy(a => a.BookingId)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(a => (a.AttachFiles ?? string.Empty)
                        .Split("##", StringSplitOptions.RemoveEmptyEntries))
                    .Count());

        var items = bookings.Select(b =>
        {
            int.TryParse(b.BookingType, out var typeId);
            typeLookup.TryGetValue(typeId, out var typeName);

            int.TryParse(b.AssignedUser, out var assignedUserId);
            userLookup.TryGetValue(assignedUserId, out var assignedUserName);

            int.TryParse(b.CreatedBy, out var createdById);
            userLookup.TryGetValue(createdById, out var createdByName);

            var bookIdKey = b.BookId.ToString();
            return new BookingSummary
            {
                BookId = b.BookId,
                ReferenceNo = b.ReferenceNo,
                BookingType = b.BookingType,
                TypeName = typeName,
                CustomerName = b.CustomerName,
                Email = b.Email,
                Phone = b.Phone,
                Airline = b.Airline,
                Pnr = b.Pnr,
                TicketStatus = b.TicketStatus,
                TotalAmount = b.TotalAmount,
                McoAmount = b.McoAmount,
                NetMco = b.NetMco,
                Currency = b.Currency,
                CreatedDate = b.CreatedDate,
                TravelDate = b.TravelDate,
                AssignedUserId = b.AssignedUser,
                AssignedUserName = assignedUserName,
                CreatedById = b.CreatedBy,
                CreatedByName = createdByName,
                CustomerAckStatus = b.CustomerAckStatus,
                McoStatus = b.McoStatus,
                QualityStatus = b.QualityStatus,
                FollowUpBy = b.FollowUpBy,
                FollowUpDate = b.FollowUpDate,
                CommentCount = commentCounts.GetValueOrDefault(bookIdKey),
                AttachmentCount = attachmentCounts.GetValueOrDefault(b.BookId)
            };
        }).ToList();

        return new PagedResult<BookingSummary>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<BookingDetail?> GetBookingByIdAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken);

        if (booking is null)
        {
            return null;
        }

        int.TryParse(booking.BookingType, out var typeId);
        var typeName = await db.BookingTypes.AsNoTracking()
            .Where(t => t.TypeId == typeId)
            .Select(t => t.TypeName)
            .FirstOrDefaultAsync(cancellationToken);

        var userIds = new List<int>();
        var assignedUserId = 0;
        var createdById = 0;

        if (int.TryParse(booking.AssignedUser, out var parsedAssigned))
        {
            assignedUserId = parsedAssigned;
            userIds.Add(parsedAssigned);
        }

        if (int.TryParse(booking.CreatedBy, out var parsedCreated))
        {
            createdById = parsedCreated;
            userIds.Add(parsedCreated);
        }

        if (booking.FollowUpBy is > 0)
        {
            userIds.Add(booking.FollowUpBy.Value);
        }

        var userLookup = userIds.Count == 0
            ? new Dictionary<int, string?>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Username, cancellationToken);

        userLookup.TryGetValue(assignedUserId, out var assignedUserName);
        userLookup.TryGetValue(createdById, out var createdByName);
        string? followUpByName = null;
        if (booking.FollowUpBy is > 0)
        {
            userLookup.TryGetValue(booking.FollowUpBy.Value, out followUpByName);
        }

        var passengers = await db.Passengers.AsNoTracking()
            .Where(p => p.BookingId == bookId)
            .OrderBy(p => p.PassId)
            .Select(p => new PassengerInfo
            {
                PassId = p.PassId,
                FirstName = p.FirstName,
                MiddleName = p.MiddleName,
                LastName = p.LastName,
                Dob = p.Dob,
                Gender = p.Gender,
                TicketNo = p.TicketNo
            })
            .ToListAsync(cancellationToken);

        var bookIdKey = bookId.ToString();
        var commentCount = await db.Comments.AsNoTracking()
            .CountAsync(c => c.BookingId == bookIdKey, cancellationToken);
        var comments = await LoadCommentsForBookingAsync(bookIdKey, 50, cancellationToken);

        var attachmentRows = await db.Attachments.AsNoTracking()
            .Where(a => a.BookingId == bookId)
            .Select(a => a.AttachFiles)
            .ToListAsync(cancellationToken);

        var attachments = attachmentRows
            .SelectMany(ParseAttachmentFileNames)
            .Distinct()
            .Select(fileName => new BookingAttachmentInfo
            {
                FileName = fileName,
                DisplayName = FormatHelper.AttachmentDisplayName(fileName),
                IsImage = IsImageFile(fileName)
            })
            .ToList();

        return new BookingDetail
        {
            BookId = booking.BookId,
            ReferenceNo = booking.ReferenceNo,
            BookingType = booking.BookingType,
            TypeName = typeName,
            CustomerName = booking.CustomerName,
            Email = booking.Email,
            Phone = booking.Phone,
            Airline = booking.Airline,
            Pnr = booking.Pnr,
            TicketStatus = booking.TicketStatus,
            TotalAmount = booking.TotalAmount,
            Currency = booking.Currency,
            CreatedDate = booking.CreatedDate,
            TravelDate = booking.TravelDate,
            ReturnDate = booking.ReturnDate,
            AssignedUserId = booking.AssignedUser,
            AssignedUserName = assignedUserName,
            CreatedById = booking.CreatedBy,
            CreatedByName = createdByName,
            FlightInfo = booking.FlightInfo,
            FlightPnr = booking.FlightPnr,
            Supplier = booking.Supplier,
            TicketCost = booking.TicketCost,
            McoAmount = booking.McoAmount,
            NetMco = booking.NetMco,
            PaymentMethod = booking.PaymentMethod,
            PaymentMode = booking.PaymentMode,
            City = booking.City,
            State = booking.State,
            Country = booking.Country,
            QualityStatus = booking.QualityStatus,
            McoStatus = booking.McoStatus,
            McoSupplier = booking.McoSupplier,
            PartialRefund = booking.PartialRefund,
            SplitCharges = booking.SplitCharges,
            PaymentUrl = booking.PaymentUrl,
            ClassType = booking.ClassType,
            BookingLang = booking.BookingLang,
            CustomEmailSubject = booking.CustomEmailSubject,
            Tfn = booking.TravelPhoneNumber,
            CardHolderName = booking.CardHolderName ?? booking.CustomerName,
            CardNumberMasked = FormatHelper.MaskCard(booking.CardNumber, booking.LastFourCardDigits),
            ExpiryDate = booking.ExpiryDate,
            InboundPhone = booking.InboundPhone,
            BillingAddress = booking.Address,
            ZipCode = booking.Zip,
            CustomerAckStatus = booking.CustomerAckStatus,
            CustomerAckIp = booking.CustomerAckIp,
            CustomerAckLocation = booking.CustomerAckLocation,
            CustomerAckDate = booking.CustomerAckDate,
            FollowUpBy = booking.FollowUpBy,
            FollowUpByName = followUpByName,
            FollowUpDate = booking.FollowUpDate,
            PassengerCount = passengers.Count,
            Passengers = passengers,
            Comments = comments,
            CommentCount = commentCount,
            AttachmentCount = attachments.Count,
            Attachments = attachments
        };
    }

    private static IEnumerable<string> ParseAttachmentFileNames(string? attachFiles)
    {
        if (string.IsNullOrWhiteSpace(attachFiles))
        {
            yield break;
        }

        foreach (var part in attachFiles.Split("##", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
    }

    public async Task<BookingCommentsViewModel?> GetBookingCommentsAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var header = await db.Bookings.AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.BookId, b.ReferenceNo, b.CustomerName })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var bookIdKey = bookId.ToString();
        var comments = await LoadCommentsForBookingAsync(bookIdKey, 200, cancellationToken);

        return new BookingCommentsViewModel
        {
            BookId = header.BookId,
            ReferenceNo = header.ReferenceNo,
            CustomerName = header.CustomerName,
            Comments = comments
        };
    }

    public async Task<bool> AddBookingCommentAsync(
        int bookId,
        string userId,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var trimmed = comment.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var exists = await db.Bookings.AsNoTracking()
            .AnyAsync(b => b.BookId == bookId, cancellationToken);

        if (!exists)
        {
            return false;
        }

        var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var bookingIdKey = bookId.ToString();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tbl_book_coments (bookingid, username, commentarea, createddate)
            VALUES ({bookingIdKey}, {userId}, {trimmed}, {createdDate})
            """, cancellationToken);

        cache.Remove(DashboardStatsCacheKey);
        return true;
    }

    private async Task<List<BookingComment>> LoadCommentsForBookingAsync(
        string bookIdKey,
        int take,
        CancellationToken cancellationToken)
    {
        var comments = await db.Comments.AsNoTracking()
            .Where(c => c.BookingId == bookIdKey)
            .OrderByDescending(c => c.Id)
            .Take(take)
            .Select(c => new BookingComment
            {
                Id = c.Id,
                Username = c.Username,
                Comment = c.Comment,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync(cancellationToken);

        await ResolveCommentAuthorsAsync(comments, cancellationToken);
        return comments;
    }

    private async Task ResolveCommentAuthorsAsync(
        List<BookingComment> comments,
        CancellationToken cancellationToken)
    {
        var authorIds = comments
            .Select(c => c.Username)
            .Where(u => !string.IsNullOrWhiteSpace(u) && int.TryParse(u, out _))
            .Select(u => int.Parse(u!))
            .Distinct()
            .ToList();

        if (authorIds.Count == 0)
        {
            foreach (var comment in comments)
            {
                comment.AuthorName = comment.Username;
            }

            return;
        }

        var authorLookup = await db.Users.AsNoTracking()
            .Where(u => authorIds.Contains(u.UserId))
            .ToDictionaryAsync(
                u => u.UserId.ToString(),
                u => u.Username ?? u.Email ?? u.UserId.ToString(),
                cancellationToken);

        foreach (var comment in comments)
        {
            if (comment.Username is not null &&
                authorLookup.TryGetValue(comment.Username, out var name))
            {
                comment.AuthorName = name;
            }
            else
            {
                comment.AuthorName = comment.Username;
            }
        }
    }

    public async Task<IReadOnlyList<AgentSummary>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.Status == "1")
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

        var bookingCounts = await db.Bookings.AsNoTracking()
            .Where(b => b.AssignedUser != null && b.AssignedUser != "")
            .GroupBy(b => b.AssignedUser!)
            .Select(g => new { AssignedUser = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countLookup = bookingCounts.ToDictionary(x => x.AssignedUser, x => x.Count);

        return users
            .Select(u => new AgentSummary
            {
                UserId = u.UserId,
                Username = u.Username,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                UserType = u.UserType,
                Status = u.Status,
                BookingCount = countLookup.GetValueOrDefault(u.UserId.ToString())
            })
            .OrderByDescending(a => a.BookingCount)
            .ThenBy(a => a.Username)
            .ToList();
    }

    public async Task<UserAccount?> ValidateUserAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var trimmedLogin = login.Trim();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Status == "1" && (u.Username == trimmedLogin || u.Email == trimmedLogin))
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FirstName,
                u.LastName,
                u.Email,
                u.UserType,
                u.PasswordHash
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.Users
            .Where(u => u.UserId == user.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLogin, now), cancellationToken);

        cache.Set(DbConnectedCacheKey, true, DbConnectedCacheTtl);

        return new UserAccount
        {
            UserId = user.UserId,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType
        };
    }

    public Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return db.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLogin, now), cancellationToken);
    }

    public async Task<IReadOnlyList<BookingTypeOption>> GetBookingTypesAsync(CancellationToken cancellationToken = default)
    {
        return await db.BookingTypes.AsNoTracking()
            .Where(t => t.Status == 1)
            .OrderBy(t => t.Border)
            .Select(t => new BookingTypeOption
            {
                Id = t.TypeId.ToString(),
                Name = t.TypeName
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GenerateNextReferenceNoAsync(CancellationToken cancellationToken = default)
    {
        var refs = await db.Bookings.AsNoTracking()
            .Where(b => b.ReferenceNo != null && b.ReferenceNo.StartsWith("RMF"))
            .Select(b => b.ReferenceNo!)
            .ToListAsync(cancellationToken);

        long max = 7_000_000;
        foreach (var reference in refs)
        {
            if (reference.Length > 3 &&
                long.TryParse(reference[3..], out var number) &&
                number > max)
            {
                max = number;
            }
        }

        return $"RMF{max + 1}";
    }

    public async Task<(int BookId, string ReferenceNo)> CreateBookingAsync(
        CreateBookingViewModel model,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var passengers = model.Passengers
            .Where(p => !string.IsNullOrWhiteSpace(p.FirstName) ||
                        !string.IsNullOrWhiteSpace(p.LastName))
            .ToList();

        if (passengers.Count == 0)
        {
            passengers = [new PassengerFormModel()];
        }

        var referenceNo = string.IsNullOrWhiteSpace(model.ReferenceNo)
            ? await GenerateNextReferenceNoAsync(cancellationToken)
            : model.ReferenceNo.Trim();

        var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var cardDigits = model.CardNumber?.Replace("-", "").Replace(" ", "");
        var lastFour = cardDigits?.Length >= 4 ? cardDigits[^4..] : null;

        double.TryParse(model.McoAmount, out var mcoAmount);
        double.TryParse(model.TotalAmount, out var totalAmount);
        double.TryParse(model.NetMco, out var netMco);

        var booking = new BookFlightDetail
        {
            ReferenceNo = referenceNo,
            BookingType = model.BookingType ?? "1",
            FlightPnr = model.FlightPnr,
            FlightInfo = model.FlightPnr,
            CustomerName = model.CardHolderName,
            Email = model.Email,
            Phone = model.BillingPhone,
            InboundPhone = model.InboundPhone,
            NoOfPeople = passengers.Count,
            CreatedDate = createdDate,
            PaymentMethod = model.CardType,
            CardHolderName = model.CardHolderName,
            CardNumber = model.CardNumber,
            LastFourCardDigits = lastFour,
            ExpiryDate = model.ExpiryDate,
            Cvv = model.Cvv,
            Address = model.BillingAddress,
            Zip = model.ZipCode,
            Pnr = model.AirlineConfirmation,
            TicketStatus = model.TicketStatus,
            Supplier = model.Supplier,
            TicketCost = string.IsNullOrWhiteSpace(model.TicketCost) ? "0" : model.TicketCost,
            Airline = model.AirlineName,
            McoAmount = mcoAmount,
            TotalAmount = totalAmount,
            McoSupplier = model.McoSupplier,
            McoStatus = model.McoStatus,
            PartialRefund = model.PartialRefund,
            NetMco = netMco,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency,
            SplitCharges = string.IsNullOrWhiteSpace(model.SplitCharges) ? "no" : model.SplitCharges,
            PaymentUrl = model.PaymentUrl,
            PaymentMode = string.IsNullOrWhiteSpace(model.PaymentMode) ? "manual" : model.PaymentMode,
            CustomEmailSubject = model.CustomEmailSubject,
            BookingLang = model.BookingLang,
            CreatedBy = userId,
            AssignedUser = userId,
            TravelDate = model.TravelDate,
            ReturnDate = model.ReturnDate,
            TravelPhoneNumber = model.Tfn,
            ClassType = model.ClassType,
            QualityStatus = "0",
            IsHide = 0
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var passenger in passengers)
        {
            db.Passengers.Add(new PassengerList
            {
                BookingId = booking.BookId,
                FirstName = passenger.FirstName,
                MiddleName = passenger.MiddleName,
                LastName = passenger.LastName,
                Gender = passenger.Gender,
                Dob = passenger.FormattedDob,
                TicketNo = passenger.TicketNo
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.Comment))
        {
            await AddBookingCommentAsync(booking.BookId, userId, model.Comment, cancellationToken);
        }

        cache.Remove(DashboardStatsCacheKey);
        return (booking.BookId, referenceNo);
    }

    public async Task<CreateBookingViewModel?> GetBookingForEditAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken);

        if (booking is null)
        {
            return null;
        }

        var passengers = await db.Passengers.AsNoTracking()
            .Where(p => p.BookingId == bookId)
            .OrderBy(p => p.PassId)
            .ToListAsync(cancellationToken);

        var bookingTypes = await GetBookingTypesAsync(cancellationToken);

        return new CreateBookingViewModel
        {
            BookId = booking.BookId,
            BookingType = booking.BookingType,
            ReferenceNo = booking.ReferenceNo,
            BookingLang = booking.BookingLang,
            CustomEmailSubject = booking.CustomEmailSubject,
            FlightPnr = booking.FlightPnr ?? booking.FlightInfo,
            TravelDate = NormalizeDateForInput(booking.TravelDate),
            ReturnDate = NormalizeDateForInput(booking.ReturnDate),
            Tfn = booking.TravelPhoneNumber,
            Passengers = passengers.Count == 0
                ? [new PassengerFormModel()]
                : passengers.Select(p =>
                {
                    ParseDob(p.Dob, out var day, out var month, out var year);
                    return new PassengerFormModel
                    {
                        PassId = p.PassId,
                        FirstName = p.FirstName,
                        MiddleName = p.MiddleName,
                        LastName = p.LastName,
                        Gender = p.Gender,
                        DobDay = day,
                        DobMonth = month,
                        DobYear = year,
                        TicketNo = p.TicketNo
                    };
                }).ToList(),
            CardHolderName = booking.CardHolderName ?? booking.CustomerName,
            CardType = booking.PaymentMethod,
            CardNumber = MaskCardNumber(booking.CardNumber, booking.LastFourCardDigits),
            ExpiryDate = booking.ExpiryDate,
            Cvv = booking.Cvv,
            InboundPhone = booking.InboundPhone,
            BillingPhone = booking.Phone,
            Email = booking.Email,
            BillingAddress = booking.Address,
            ZipCode = booking.Zip,
            AirlineConfirmation = booking.Pnr,
            TicketStatus = booking.TicketStatus ?? "0",
            ClassType = booking.ClassType,
            Supplier = booking.Supplier,
            TicketCost = booking.TicketCost,
            AirlineName = booking.Airline,
            McoAmount = booking.McoAmount.ToString("0.##"),
            TotalAmount = booking.TotalAmount.ToString("0.##"),
            SplitCharges = booking.SplitCharges,
            McoSupplier = booking.McoSupplier,
            McoStatus = booking.McoStatus ?? "0",
            PartialRefund = booking.PartialRefund,
            NetMco = booking.NetMco.ToString("0.##"),
            Currency = booking.Currency,
            PaymentUrl = booking.PaymentUrl,
            PaymentMode = booking.PaymentMode,
            BookingTypes = bookingTypes
        };
    }

    public async Task<bool> UpdateBookingAsync(
        CreateBookingViewModel model,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (model.BookId is not > 0)
        {
            return false;
        }

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.BookId == model.BookId, cancellationToken);

        if (booking is null)
        {
            return false;
        }

        var passengers = model.Passengers
            .Where(p => !string.IsNullOrWhiteSpace(p.FirstName) ||
                        !string.IsNullOrWhiteSpace(p.LastName))
            .ToList();

        if (passengers.Count == 0)
        {
            return false;
        }

        var cardDigits = model.CardNumber?.Replace("-", "").Replace(" ", "");
        var lastFour = cardDigits?.Length >= 4 ? cardDigits[^4..] : booking.LastFourCardDigits;

        double.TryParse(model.McoAmount, out var mcoAmount);
        double.TryParse(model.TotalAmount, out var totalAmount);
        double.TryParse(model.NetMco, out var netMco);

        booking.BookingType = model.BookingType ?? booking.BookingType;
        booking.FlightPnr = model.FlightPnr;
        booking.FlightInfo = model.FlightPnr;
        booking.CustomerName = model.CardHolderName;
        booking.Email = model.Email;
        booking.Phone = model.BillingPhone;
        booking.InboundPhone = model.InboundPhone;
        booking.NoOfPeople = passengers.Count;
        booking.PaymentMethod = model.CardType;
        booking.CardHolderName = model.CardHolderName;

        if (!string.IsNullOrWhiteSpace(model.CardNumber) && !model.CardNumber.Contains('•'))
        {
            booking.CardNumber = model.CardNumber;
            booking.LastFourCardDigits = lastFour;
        }

        booking.ExpiryDate = model.ExpiryDate;
        booking.Cvv = model.Cvv;
        booking.Address = model.BillingAddress;
        booking.Zip = model.ZipCode;
        booking.Pnr = model.AirlineConfirmation;
        booking.TicketStatus = model.TicketStatus;
        booking.Supplier = model.Supplier;
        booking.TicketCost = string.IsNullOrWhiteSpace(model.TicketCost) ? "0" : model.TicketCost;
        booking.Airline = model.AirlineName;
        booking.McoAmount = mcoAmount;
        booking.TotalAmount = totalAmount;
        booking.McoSupplier = model.McoSupplier;
        booking.McoStatus = model.McoStatus;
        booking.PartialRefund = model.PartialRefund;
        booking.NetMco = netMco;
        booking.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency;
        booking.SplitCharges = string.IsNullOrWhiteSpace(model.SplitCharges) ? "no" : model.SplitCharges;
        booking.PaymentUrl = model.PaymentUrl;
        booking.PaymentMode = string.IsNullOrWhiteSpace(model.PaymentMode) ? "manual" : model.PaymentMode;
        booking.CustomEmailSubject = model.CustomEmailSubject;
        booking.BookingLang = model.BookingLang;
        booking.TravelDate = model.TravelDate;
        booking.ReturnDate = model.ReturnDate;
        booking.TravelPhoneNumber = model.Tfn;
        booking.ClassType = model.ClassType;
        booking.AssignedUser = userId;

        var existingPassengers = await db.Passengers
            .Where(p => p.BookingId == booking.BookId)
            .ToListAsync(cancellationToken);

        var submittedIds = passengers
            .Where(p => p.PassId > 0)
            .Select(p => p.PassId)
            .ToHashSet();

        db.Passengers.RemoveRange(existingPassengers.Where(p => !submittedIds.Contains(p.PassId)));

        foreach (var passenger in passengers)
        {
            if (passenger.PassId > 0)
            {
                var existing = existingPassengers.FirstOrDefault(p => p.PassId == passenger.PassId);
                if (existing is null)
                {
                    continue;
                }

                existing.FirstName = passenger.FirstName;
                existing.MiddleName = passenger.MiddleName;
                existing.LastName = passenger.LastName;
                existing.Gender = passenger.Gender;
                existing.Dob = passenger.FormattedDob;
                existing.TicketNo = passenger.TicketNo;
            }
            else
            {
                db.Passengers.Add(new PassengerList
                {
                    BookingId = booking.BookId,
                    FirstName = passenger.FirstName,
                    MiddleName = passenger.MiddleName,
                    LastName = passenger.LastName,
                    Gender = passenger.Gender,
                    Dob = passenger.FormattedDob,
                    TicketNo = passenger.TicketNo
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.Comment))
        {
            await AddBookingCommentAsync(booking.BookId, userId, model.Comment, cancellationToken);
        }

        cache.Remove(DashboardStatsCacheKey);
        return true;
    }

    private static string? NormalizeDateForInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd");
        }

        return value;
    }

    private static string? MaskCardNumber(string? cardNumber, string? lastFour)
    {
        if (!string.IsNullOrWhiteSpace(lastFour))
        {
            return $"••••-••••-••••-{lastFour}";
        }

        return cardNumber;
    }

    private static void ParseDob(string? dob, out int? day, out int? month, out int? year)
    {
        day = null;
        month = null;
        year = null;

        if (string.IsNullOrWhiteSpace(dob))
        {
            return;
        }

        var parts = dob.Split('/');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var m) &&
            int.TryParse(parts[1], out var d) &&
            int.TryParse(parts[2], out var y))
        {
            month = m;
            day = d;
            year = y;
        }
    }
    public async Task<IReadOnlyList<AgentPerformanceViewModel>> GetAgentPerformanceAsync(
    DateTime? fromDate,
    DateTime? toDate,
    CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.Status == "1")
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

        var bookings = db.Bookings.AsNoTracking()
            .Where(b => !string.IsNullOrEmpty(b.AssignedUser));

        // Date Filter
        if (fromDate.HasValue)
        {
            bookings = bookings.Where(b =>
                !string.IsNullOrEmpty(b.CreatedDate) &&
                DateTime.Parse(b.CreatedDate) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var endDate = toDate.Value.Date.AddDays(1);

            bookings = bookings.Where(b =>
                !string.IsNullOrEmpty(b.CreatedDate) &&
                DateTime.Parse(b.CreatedDate) < endDate);
        }

        var bookingSummary = await bookings
            .GroupBy(b => b.AssignedUser!)
            .Select(g => new
            {
                AssignedUser = g.Key,
                BookingCount = g.Count(),
                GrossMco = g.Sum(x => x.McoAmount),
                NetMco = g.Sum(x => x.NetMco)
            })
            .ToListAsync(cancellationToken);

        var lookup = bookingSummary.ToDictionary(x => x.AssignedUser);

        return users
            .Select(u =>
            {
                lookup.TryGetValue(u.UserId.ToString(), out var summary);

                return new AgentPerformanceViewModel
                {
                    UserId = u.UserId,
                    AgentName = u.Username,
                    Bookings = summary?.BookingCount ?? 0,
                    GrossMco = summary == null ? 0 : Convert.ToDecimal(summary.GrossMco),
                    NetMco = summary == null ? 0 : Convert.ToDecimal(summary.NetMco)
                };
            })
            .OrderByDescending(x => x.Bookings)
            .ThenBy(x => x.AgentName)
            .ToList();
    }
}
