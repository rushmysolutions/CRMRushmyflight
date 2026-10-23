using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.ViewModels;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using static RushMyBookings.Crm.Models.UserAccount;

namespace RushMyBookings.Crm.Services;

public sealed class CrmDataService(CrmDbContext db, IMemoryCache cache) : ICrmDataService
{
    private const string DbConnectedCacheKey = "crm:db-connected";
    private const string DashboardStatsCacheKey = "crm:dashboard-stats";
    private static readonly TimeSpan DbConnectedCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DashboardStatsCacheTtl = TimeSpan.FromMinutes(3);

    public async Task<bool> CanConnectAsync()
    {
        if (cache.TryGetValue(DbConnectedCacheKey, out bool cached))
        {
            return cached;
        }

        var connected = await ProbeDatabaseConnectionAsync();
        cache.Set(
            DbConnectedCacheKey,
            connected,
            connected ? DbConnectedCacheTtl : TimeSpan.FromSeconds(15));

        return connected;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        if (cache.TryGetValue(DashboardStatsCacheKey, out DashboardStats? cached) && cached is not null)
        {
            return cached;
        }

        var stats = await LoadDashboardStatsAsync();
        cache.Set(DashboardStatsCacheKey, stats, DashboardStatsCacheTtl);
        cache.Set(DbConnectedCacheKey, true, DbConnectedCacheTtl);
        return stats;
    }

    private async Task<bool> ProbeDatabaseConnectionAsync()
    {
        try
        {
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private async Task<DashboardStats> LoadDashboardStatsAsync()
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
            .FirstAsync();

        var typeLookup = await db.BookingTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.TypeId.ToString(), t => t.TypeName ?? "Unknown");

        var bookingsByType = await db.Bookings.AsNoTracking()
            .Where(b => b.IsHide == 0)
            .GroupBy(b => b.BookingType ?? "0")
            .Select(g => new { BookingType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToListAsync();

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
            .ToListAsync();

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
        IReadOnlyDictionary<int, string>? typeLookup = null, BookingAccessScope? scope = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Bookings.AsNoTracking().Where(b => b.IsHide == 0);

        var ownOnly = scope?.OwnOnly == true;
        var bookingNo = filter.BookingNo?.Trim();

        if (!string.IsNullOrEmpty(bookingNo))
        {
            // Agents: exact booking number only (can open someone else's booking this way)
            // Admins: partial search as before
            query = ownOnly
                ? query.Where(b => b.ReferenceNo == bookingNo)
                : query.Where(b => b.ReferenceNo != null && b.ReferenceNo.Contains(bookingNo));
        }
        else if (ownOnly)
        {
            var uid = string.IsNullOrWhiteSpace(scope!.UserId) ? "-1" : scope.UserId!;
            query = query.Where(b => b.CreatedBy == uid);
        }


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

        var totalCount = await query.CountAsync();

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
            .ToListAsync();

        typeLookup ??= await db.BookingTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.TypeId, t => t.TypeName ?? "Unknown");

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
                .ToDictionaryAsync(u => u.UserId, u => u.Username);

        var bookIds = bookings.Select(b => b.BookId).ToList();
        var bookIdStrings = bookIds.Select(id => id.ToString()).ToList();
        var commentCounts = bookIds.Count == 0
            ? new Dictionary<string, int>()
            : await db.Comments.AsNoTracking()
                .Where(c => bookIdStrings.Contains(c.BookingId))
                .GroupBy(c => c.BookingId)
                .Select(g => new { BookingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BookingId, x => x.Count);

        var attachmentRows = bookIds.Count == 0
            ? []
            : await db.Attachments.AsNoTracking()
                .Where(a => bookIds.Contains(a.BookingId))
                .Select(a => new { a.BookingId, a.AttachFiles })
                .ToListAsync();

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

    public async Task<BookingDetail?> GetBookingByIdAsync(int bookId)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookId == bookId);

        if (booking is null)
        {
            return null;
        }

        int.TryParse(booking.BookingType, out var typeId);
        var typeName = await db.BookingTypes.AsNoTracking()
            .Where(t => t.TypeId == typeId)
            .Select(t => t.TypeName)
            .FirstOrDefaultAsync();

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
                .ToDictionaryAsync(u => u.UserId, u => u.Username);

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
            .ToListAsync();

        var bookIdKey = bookId.ToString();
        var commentCount = await db.Comments.AsNoTracking()
            .CountAsync(c => c.BookingId == bookIdKey);
        var comments = await LoadCommentsForBookingAsync(bookIdKey, 50);

        var attachmentRows = await db.Attachments.AsNoTracking()
            .Where(a => a.BookingId == bookId)
            .Select(a => a.AttachFiles)
            .ToListAsync();

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

        McoSupplier? mcoSupplier = null;
        if (int.TryParse(booking.McoSupplier, out var mcoSupplierId))
        {
            mcoSupplier = await db.McoSuppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == mcoSupplierId);
        }
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
            Attachments = attachments,
            McoSupplierName = mcoSupplier?.Name,
            McoSupplierPhone = mcoSupplier?.TollFree,
            McoSupplierFromEmail = mcoSupplier?.FromEmail,
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

    public async Task<BookingCommentsViewModel?> GetBookingCommentsAsync(int bookId)
    {
        var header = await db.Bookings.AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.BookId, b.ReferenceNo, b.CustomerName })
            .FirstOrDefaultAsync();

        if (header is null)
        {
            return null;
        }

        var bookIdKey = bookId.ToString();
        var comments = await LoadCommentsForBookingAsync(bookIdKey, 200);

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
        string comment)
    {
        var trimmed = comment.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var exists = await db.Bookings.AsNoTracking()
            .AnyAsync(b => b.BookId == bookId);

        if (!exists)
        {
            return false;
        }

        var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var bookingIdKey = bookId.ToString();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tbl_book_coments (bookingid, username, commentarea, createddate)
            VALUES ({bookingIdKey}, {userId}, {trimmed}, {createdDate})
            """);

        cache.Remove(DashboardStatsCacheKey);
        return true;
    }

    private async Task<List<BookingComment>> LoadCommentsForBookingAsync(
        string bookIdKey,
        int take)
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
            .ToListAsync();

        await ResolveCommentAuthorsAsync(comments);
        return comments;
    }

    private async Task ResolveCommentAuthorsAsync(
        List<BookingComment> comments)
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
                u => u.Username ?? u.Email ?? u.UserId.ToString());

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

    public async Task<IReadOnlyList<AgentSummary>> GetAgentsAsync()
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.Status == "1")
            .OrderBy(u => u.Username)
            .ToListAsync();

        var bookingCounts = await db.Bookings.AsNoTracking()
            .Where(b => b.AssignedUser != null && b.AssignedUser != "")
            .GroupBy(b => b.AssignedUser!)
            .Select(g => new { AssignedUser = g.Key, Count = g.Count() })
            .ToListAsync();

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

    public async Task<LoginResult> ValidateUserAsync(string login, string password, string? clientIp)
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
                u.PasswordHash,
                u.AllowedIps
            })
            .FirstOrDefaultAsync();

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // Password is correct — now check IP (superadmin is never blocked)
        var isSuperAdmin = string.Equals(user.UserType, "superadmin", StringComparison.OrdinalIgnoreCase);
        if (!isSuperAdmin && !IsIpAllowed(user.AllowedIps, clientIp))
        {
            return new LoginResult(LoginStatus.IpNotAllowed);
        }

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.Users
            .Where(u => u.UserId == user.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLogin, now));

        cache.Set(DbConnectedCacheKey, true, DbConnectedCacheTtl);

        return new LoginResult(LoginStatus.Success, new UserAccount
        {
            UserId = user.UserId,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType
        });
    }
    private static bool IsIpAllowed(string? allowedIps, string? clientIp)
    {
        // No IPs configured = no restriction (same as leaving the field empty)
        if (string.IsNullOrWhiteSpace(allowedIps)) return true;
        if (string.IsNullOrWhiteSpace(clientIp) || !IPAddress.TryParse(clientIp, out var client)) return false;

        return allowedIps
            .Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(ip => IPAddress.TryParse(ip, out var allowed) && allowed.Equals(client));
    }
    public Task UpdateLastLoginAsync(int userId)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return db.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLogin, now));
    }

    public async Task<IReadOnlyList<BookingTypeOption>> GetBookingTypesAsync()
    {
        return await db.BookingTypes.AsNoTracking()
            .Where(t => t.Status == 1)
            .OrderBy(t => t.Border)
            .Select(t => new BookingTypeOption
            {
                Id = t.TypeId.ToString(),
                Name = t.TypeName
            })
            .ToListAsync();
    }

    public async Task<string> GenerateNextReferenceNoAsync()
    {
        var refs = await db.Bookings.AsNoTracking()
            .Where(b => b.ReferenceNo != null && b.ReferenceNo.StartsWith("RMF"))
            .Select(b => b.ReferenceNo!)
            .ToListAsync();

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
        string userId)
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
            ? await GenerateNextReferenceNoAsync()
            : model.ReferenceNo.Trim();

        var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var cardDigits = model.CardNumber?.Replace("-", "").Replace(" ", "");
        var lastFour = cardDigits?.Length >= 4 ? cardDigits[^4..] : null;

        var (total, net) = BookingAmountCalculator.Calculate(model.TicketCost, model.McoAmount);
        var mcoAmount = (double)BookingAmountCalculator.Parse(model.McoAmount);
        var totalAmount = (double)total;
        var netMco = (double)net;

        var booking = new BookFlightDetail
        {
            ReferenceNo = referenceNo,
            BookingType = model.BookingType ?? "1",
            //FlightPnr = model.FlightPnr,
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
        await db.SaveChangesAsync();

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

        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(model.Comment))
        {
            await AddBookingCommentAsync(booking.BookId, userId, model.Comment);
        }

        cache.Remove(DashboardStatsCacheKey);
        return (booking.BookId, referenceNo);
    }

    public async Task<CreateBookingViewModel?> GetBookingForEditAsync(int bookId)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookId == bookId);

        if (booking is null)
        {
            return null;
        }

        var passengers = await db.Passengers.AsNoTracking()
            .Where(p => p.BookingId == bookId)
            .OrderBy(p => p.PassId)
            .ToListAsync();

        var bookingTypes = await GetBookingTypesAsync();

        var attachmentRows = await db.Attachments.AsNoTracking()
            .Where(a => a.BookingId == bookId)
            .Select(a => a.AttachFiles)
            .ToListAsync();

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
        return new CreateBookingViewModel
        { Attachments = attachments,
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
            CardNumber = booking.CardNumber, /*MaskCardNumber(booking.CardNumber, booking.LastFourCardDigits)*/
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
            BookingTypes = bookingTypes,
            McoSuppliers = await GetMcoSuppliersAsync(),


        };
    }

    public async Task<bool> UpdateBookingAsync(
        CreateBookingViewModel model,
        string userId)
    {
        if (model.BookId is not > 0)
        {
            return false;
        }

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.BookId == model.BookId);

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
        //booking.FlightPnr = model.FlightPnr;
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
            .ToListAsync();

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

        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(model.Comment))
        {
            await AddBookingCommentAsync(booking.BookId, userId, model.Comment);
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
    DateTime? toDate, int? onlyUserId = null)
    {
        var usersQuery = db.Users.AsNoTracking().Where(u => u.Status == "1");
        var bookings = db.Bookings.AsNoTracking()
            .Where(b => !string.IsNullOrEmpty(b.AssignedUser));

        if (onlyUserId.HasValue)
        {
            var key = onlyUserId.Value.ToString();
            usersQuery = usersQuery.Where(u => u.UserId == onlyUserId.Value);
            bookings = bookings.Where(b => b.AssignedUser == key);
        }

        var users = await usersQuery.OrderBy(u => u.Username).ToListAsync();



        //var users = await db.Users.AsNoTracking()
        //    .Where(u => u.Status == "1")
        //    .OrderBy(u => u.Username)
        //    .ToListAsync();

        //var bookings = db.Bookings.AsNoTracking()
        //    .Where(b => !string.IsNullOrEmpty(b.AssignedUser));

        // Date Filter
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date.ToString("yyyy-MM-dd 00:00:00");
            bookings = bookings.Where(b =>
                b.CreatedDate != null && string.Compare(b.CreatedDate, from) >= 0);
        }

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1).ToString("yyyy-MM-dd 00:00:00");
            bookings = bookings.Where(b =>
                b.CreatedDate != null && string.Compare(b.CreatedDate, endExclusive) < 0);
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
            .ToListAsync();

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


    public async Task<IReadOnlyList<Tfn>> GetTfnsAsync() =>
    await db.Tfns.AsNoTracking().OrderByDescending(t => t.TfnId).ToListAsync();

    public Task<Tfn?> GetTfnAsync(int id) =>
        db.Tfns.AsNoTracking().FirstOrDefaultAsync(t => t.TfnId == id);

    public Task<bool> TfnExistsAsync(string tfnNumber, int excludeId = 0) =>
        db.Tfns.AnyAsync(t => t.TfnNumber == tfnNumber && t.TfnId != excludeId);

    public async Task SaveTfnAsync(Tfn tfn)
    {
        if (tfn.TfnId == 0)
        {
            //tfn.CreatedDate = DateTime.Now;
            db.Tfns.Add(tfn);
        }
        else
        {
            var existing = await db.Tfns.FindAsync(tfn.TfnId)
                           ?? throw new KeyNotFoundException("TFN not found");
            existing.TfnNumber = tfn.TfnNumber;
            existing.DidNumber = tfn.DidNumber;
            existing.Type = tfn.Type;
            existing.IsActive = tfn.IsActive;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteTfnAsync(int id)
    {
        var tfn = await db.Tfns.FindAsync(id);
        if (tfn is null) return;
        db.Tfns.Remove(tfn);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Tfn>> GetActiveTfnsAsync() =>
    await db.Tfns.AsNoTracking()
        .Where(t => t.IsActive == true)      // converter se tfn_status = '1' banega
        .OrderBy(t => t.TfnNumber)
        .ToListAsync();


    public async Task<(IReadOnlyList<AgentListItem> Items, int Total)> SearchAgentsAsync(
    string? name, string? phone, string? email, int page, int pageSize, int? onlyUserId = null)
    {
        var q = db.Users.AsNoTracking().Where(u => u.UserType != "superadmin");

        if (onlyUserId.HasValue)
            q = q.Where(u => u.UserId == onlyUserId.Value);

        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(u => u.FirstName!.Contains(name) || u.LastName!.Contains(name) || u.Username!.Contains(name));
        if (!string.IsNullOrWhiteSpace(phone))
            q = q.Where(u => u.Phone!.Contains(phone));
        if (!string.IsNullOrWhiteSpace(email))
            q = q.Where(u => u.Email!.Contains(email));

        var total = await q.CountAsync();

        var users = await q.OrderByDescending(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = users.Select(u => new AgentListItem
        {
            UserId = u.UserId,
            Username = u.Username,
            DialerId = u.DialerId is "0" or "" ? null : u.DialerId,
            Password = u.CopyPass,
            Name = $"{u.FirstName} {u.LastName}".Trim(),
            Phone = u.Phone,
            Email = u.Email,
            UserType = u.UserType,
            CreatedDate = u.CreatedDate is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(u.CreatedDate.Value).ToString("MM-dd-yyyy")
                : "",
            IsActive = u.Status == "1"
        }).ToList();

        return (items, total);
    }

    public async Task<AgentFormViewModel?> GetAgentFormAsync(int id)
    {
        var u = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == id && x.UserType != "superadmin");
        if (u is null) return null;

        return new AgentFormViewModel
        {
            UserId = u.UserId,
            FirstName = u.FirstName ?? "",
            LastName = u.LastName,
            TollFreeNo = u.TollFreeNo,
            DirectLine = u.DirectLine,
            Extn = u.Extn,
            Phone = u.Phone ?? "",
            Email = u.Email,
            Dob = u.Dob,
            Sex = u.Sex ?? 0,
            Address = u.Address,
            City = u.City,
            State = u.State,
            ZipCode = u.ZipCode,
            Country = u.Country ?? "US",
            UserType = u.UserType ?? "Agent",
            Username = u.Username ?? "",
            DialerId = u.DialerId is "0" ? "" : u.DialerId
        };
    }

    public async Task<Dictionary<string, string>> ValidateAgentAsync(AgentFormViewModel m)
    {
        var errors = new Dictionary<string, string>();
        var current = m.UserId > 0
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == m.UserId)
            : null;

        if (await db.Users.AnyAsync(u => u.Username == m.Username && u.UserId != m.UserId))
            errors[nameof(m.Username)] = "Ye username pehle se exist karta hai";

        // Purane data mein duplicate email/dialer hain, isliye sirf naya ya badla hua value check hota hai
        if (!string.IsNullOrWhiteSpace(m.Email) && m.Email != current?.Email &&
            await db.Users.AnyAsync(u => u.Email == m.Email && u.UserId != m.UserId))
            errors[nameof(m.Email)] = "Ye email pehle se use ho raha hai";

        if (!string.IsNullOrWhiteSpace(m.DialerId) && m.DialerId != "0" && m.DialerId != current?.DialerId &&
            await db.Users.AnyAsync(u => u.DialerId == m.DialerId && u.UserId != m.UserId))
            errors[nameof(m.DialerId)] = "Ye Dialer ID pehle se assigned hai";

        return errors;
    }


    public async Task ToggleAgentStatusAsync(int id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserId == id && x.UserType != "superadmin");
        if (u is null) return;

        u.Status = u.Status == "1" ? "0" : "1";
        await db.SaveChangesAsync();
    }

    private static string GeneratePassword(int length = 8) =>
        RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@#", length);

    private static string Sha1(string value) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public async Task<AgentDetailsViewModel?> GetAgentDetailsAsync(int id)
    {
        var form = await GetAgentFormAsync(id);   // already excludes superadmin
        if (form is null) return null;

        var u = await db.Users.AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => new { x.CopyPass, x.AllowedIps, x.CreatedDate, x.Status })
            .FirstAsync();

        return new AgentDetailsViewModel
        {
            Agent = form,
            Password = u.CopyPass,
            AllowedIps = (u.AllowedIps ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList(),
            CreatedDate = u.CreatedDate is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(u.CreatedDate.Value).ToString("MM-dd-yyyy")
                : "",
            IsActive = u.Status == "1"
        };
    }

    public Task<string?> GetAgentUsernameAsync(int id) =>
    db.Users.AsNoTracking()
        .Where(u => u.UserId == id && u.UserType != "superadmin")
        .Select(u => u.Username)
        .FirstOrDefaultAsync();

    public async Task<bool> ChangeAgentPasswordAsync(int id, string newPassword)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id && u.UserType != "superadmin");
        if (user is null) return false;

        user.PasswordHash = Sha1(newPassword);
        user.CopyPass = newPassword;
        await db.SaveChangesAsync();
        return true;
    }

    //public async Task<IReadOnlyList<McoSupplierListItem>> GetMcoSuppliersAsync() =>
    //await db.McoSuppliers.AsNoTracking()
    //    .OrderByDescending(s => s.Id)
    //    .Select(s => new McoSupplierListItem
    //    {
    //        Id = s.Id,
    //        Name = s.Name ?? "",
    //        FromEmail = s.FromEmail,
    //        IsActive = s.IsActive
    //    })
    //    .ToListAsync();


    public async Task<IReadOnlyList<McoSupplierOption>> GetMcoSuppliersAsync()
    {
        return await db.McoSuppliers.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .Select(s => new McoSupplierOption
            {
                Id = s.Id.ToString(),
                Name = s.Name,
                FromEmail = s.FromEmail,
                IsActive = s.IsActive
            })
            .ToListAsync();
    }
    public async Task DeleteMcoSupplierAsync(int id)
    {
        var s = await db.McoSuppliers.FindAsync(id);
        if (s is null) return;
        db.McoSuppliers.Remove(s);
        await db.SaveChangesAsync();
    }

    public async Task ToggleMcoSupplierStatusAsync(int id)
    {
        var s = await db.McoSuppliers.FindAsync(id);
        if (s is null) return;
        s.IsActive = !s.IsActive;
        await db.SaveChangesAsync();
    }

    public Task<McoSupplierFormViewModel?> GetMcoSupplierFormAsync(int id) =>
    db.McoSuppliers.AsNoTracking()
        .Where(s => s.Id == id)
        .Select(s => new McoSupplierFormViewModel
        {
            Id = s.Id,
            Name = s.Name ?? "",
            TicketCharge = s.TicketCharge ?? "",
            TollFree = s.TollFree ?? "",
            Email = s.Email ?? "",
            FromEmail = s.FromEmail ?? "",
            SmtpPass = s.FromSmtpPass ?? "",
            IsActive = s.IsActive,

        })
        .FirstOrDefaultAsync();

    public async Task<bool> UpdateMcoSupplierAsync(McoSupplierFormViewModel m)
    {
        var s = await db.McoSuppliers.FindAsync(m.Id);
        if (s is null) return false;

        s.Name = m.Name.Trim();
        s.TicketCharge = m.TicketCharge.Trim();
        s.TollFree = m.TollFree.Trim();
        s.Email = m.Email.Trim();
        s.FromEmail = m.FromEmail.Trim();
        s.IsActive = m.IsActive;

        s.FromSmtpPass = m.SmtpPass.Trim();

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<McoSupplierOption>> GetMcoSupplierOptionsAsync()
    {
        return await db.McoSuppliers.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .Select(s => new McoSupplierOption
            {
                Id = s.Id.ToString(),
                Name = s.Name,
                FromEmail = s.FromEmail,
                IsActive = s.IsActive
            })
            .ToListAsync();
    }


    public async Task<McoSupplier?> GetMcoSupplierForMailAsync(int id)
    {
        return await db.McoSuppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    // data service
    public async Task<bool> AcknowledgeBookingAsync(int bookId, string? ip, string? location)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookId == bookId);
        if (booking is null)
        {
            return false;
        }

        if (booking.CustomerAckStatus == 1)
        {
            return true;   // already acknowledged: keep the first IP/date
        }

        booking.CustomerAckStatus = 1;
        booking.CustomerAckIp = ip;
        booking.CustomerAckLocation = location;
        booking.CustomerAckDate = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");   // same format as the old CRM

        await db.SaveChangesAsync();
        cache.Remove(DashboardStatsCacheKey);
        return true;
    }
    public async Task<AgentSaveResult> SaveAgentAsync(AgentFormViewModel model)
    {
        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        var editId = model.UserId is int id && id > 0 ? id : 0;
        var username = Clean(model.Username) ?? "";
        var email = Clean(model.Email);
        var dialerId = Clean(model.DialerId);
        var userType = Clean(model.UserType) ?? "Agent";

        // Never allow creating/promoting to superadmin from this form
        if (userType.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
            !AgentFormViewModel.UserTypes.Contains(userType, StringComparer.OrdinalIgnoreCase))
        {
            return AgentSaveResult.Fail(nameof(model.UserType), "Invalid user type.");
        }

        // ---- Uniqueness checks (exclude the user being edited) ----
        if (await db.Users.AnyAsync(u => u.UserId != editId && u.Username == username))
            return AgentSaveResult.Fail(nameof(model.Username), "This username is already taken.");

        if (email is not null &&
            await db.Users.AnyAsync(u => u.UserId != editId && u.Email == email))
            return AgentSaveResult.Fail(nameof(model.Email), "This email is already used by another user.");

        if (dialerId is not null && dialerId != "0" &&
            await db.Users.AnyAsync(u => u.UserId != editId && u.DialerId == dialerId))
            return AgentSaveResult.Fail(nameof(model.DialerId), "This Dialer ID is already assigned to another user.");

        CrmUser user;
        string? generatedPassword = null;

        if (editId > 0)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.UserId == editId && u.UserType != "superadmin")
                   ?? null!;
            if (user is null)
                return AgentSaveResult.Fail(string.Empty, "Agent not found.");
        }
        else
        {
            generatedPassword = GeneratePassword();
            user = new CrmUser
            {
                PasswordHash = PasswordHasher.Hash(generatedPassword),
                CopyPass = generatedPassword,          // same as old PHP panel
                Status = "1",
                CreatedDate = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            db.Users.Add(user);
        }

        // ---- Fields from the form (password, status, last login, allowed IPs untouched on edit) ----
        user.FirstName = Clean(model.FirstName);
        user.LastName = Clean(model.LastName);
        user.Phone = Clean(model.Phone);
        user.Email = email;
        user.Dob = Clean(model.Dob);
        user.Sex = (short?)model.Sex;
        user.TollFreeNo = Clean(model.TollFreeNo);
        user.DirectLine = Clean(model.DirectLine);
        user.Extn = Clean(model.Extn);
        user.Address = Clean(model.Address);
        user.City = Clean(model.City);
        user.State = Clean(model.State);
        user.ZipCode = Clean(model.ZipCode);
        user.Country = Clean(model.Country) ?? "US";
        user.UserType = userType;
        user.Username = username;
        user.DialerId = dialerId ?? "0";        // column is NOT NULL DEFAULT '0'

        await db.SaveChangesAsync();

        return new AgentSaveResult
        {
            Success = true,
            UserId = user.UserId,
            Created = editId == 0,
            GeneratedPassword = generatedPassword
        };
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@#$";
        return System.Security.Cryptography.RandomNumberGenerator.GetString(chars, 10);
    }




    public async Task<IReadOnlyList<LatestCommentItem>> GetLatestCommentsAsync(int hours, int? onlyUserId = null)
    {
        // New comments are saved with DateTime.Now (server clock); old PHP ones used US Eastern.
        // Take the earlier of the two clocks so nothing from the last 48h is missed.
        var cutoff = Min(DateTime.Now, EasternNow()).AddHours(-hours).ToString("yyyy-MM-dd HH:mm:ss");

        var rows = await db.Database.SqlQuery<LatestCommentRow>($"""
        SELECT cid         AS Cid,
               bookingid   AS BookingId,
               username    AS UserId,
               commentarea AS CommentText,
               createddate AS CreatedDate
        FROM tbl_book_coments
        WHERE createddate >= {cutoff}
        ORDER BY cid DESC
        """).ToListAsync();

        if (rows.Count == 0) return [];

        var bookIds = rows
            .Select(r => int.TryParse(r.BookingId, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var bookingsQuery = db.Bookings.AsNoTracking()
            .Where(b => b.IsHide == 0 && bookIds.Contains(b.BookId));

        if (onlyUserId.HasValue)
        {
            var key = onlyUserId.Value.ToString();
            bookingsQuery = bookingsQuery.Where(b => b.CreatedBy == key);   // agent: own bookings only
        }

        var bookings = await bookingsQuery
            .Select(b => new { b.BookId, b.ReferenceNo, b.CustomerName, b.CreatedBy })
            .ToDictionaryAsync(b => b.BookId);

        var userIds = rows.Select(r => r.UserId)
            .Concat(bookings.Values.Select(b => b.CreatedBy))
            .Select(id => int.TryParse(id, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();

        var users = userIds.Count == 0
            ? new Dictionary<int, string?>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Username);

        string? UserName(string? id) =>
            int.TryParse(id, out var n) && users.TryGetValue(n, out var name) ? name : id;

        return rows
            .Where(r => int.TryParse(r.BookingId, out var id) && bookings.ContainsKey(id))
            .Select(r =>
            {
                var b = bookings[int.Parse(r.BookingId)];
                return new LatestCommentItem
                {
                    BookId = b.BookId,
                    ReferenceNo = b.ReferenceNo,
                    CustomerName = b.CustomerName,
                    Comment = r.CommentText,
                    BookingCreatedBy = UserName(b.CreatedBy),
                    CommentCreatedBy = UserName(r.UserId),
                    CreatedDate = DateTime.TryParseExact(r.CreatedDate, "yyyy-MM-dd HH:mm:ss",
                                      CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                                  ? d.ToString("dd MMM yyyy")
                                  : r.CreatedDate ?? ""
                };
            })
            .ToList();
    }

    public async Task<(bool Exists, string? CreatedBy)> GetBookingOwnerAsync(int bookId)
    {
        var row = await db.Bookings.AsNoTracking()
            .Where(b => b.BookId == bookId && b.IsHide == 0)
            .Select(b => new { b.CreatedBy })
            .FirstOrDefaultAsync();
        return (row is not null, row?.CreatedBy);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime EasternNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        }
    }

    public async Task<(string? Ticket, string? Mco)?> GetBookingStatusesAsync(int? bookId)
    {
        var row = await db.Bookings.AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.TicketStatus, b.McoStatus })
            .FirstOrDefaultAsync();
        return row is null ? null : (row.TicketStatus, row.McoStatus);
    }


    public async Task<BookingReportOptions> GetBookingReportOptionsAsync()
    {
        var agents = await db.Users.AsNoTracking()
            .Where(u => u.Status == "1" && u.UserType != "superadmin")
            .OrderBy(u => u.Username)
            .Select(u => new ReportOption { Value = u.UserId.ToString(), Label = u.Username ?? "" })
            .ToListAsync();

        var suppliers = await db.Database.SqlQuery<ReportOption>($"""
        SELECT CAST(id AS CHAR) AS Value,
               CONCAT(COALESCE(mcosuppliername, ''),
                      CASE WHEN mcosupplierstatus = '1' THEN '' ELSE ' (In-Active)' END) AS Label
        FROM tbl_mco_supplier
        ORDER BY mcosupplierstatus DESC, mcosupplierorder, mcosuppliername
        """).ToListAsync();

        var tfns = await db.Database.SqlQuery<ReportOption>($"""
        SELECT tfn_name AS Value,
               CONCAT(tfn_name, CASE WHEN tfn_status = '1' THEN '' ELSE ' (In-Active)' END) AS Label
        FROM tbl_tfn
        WHERE tfn_name IS NOT NULL AND tfn_name <> ''
        ORDER BY tfn_status DESC, tfn_name
        """).ToListAsync();

        return new BookingReportOptions { Agents = agents, McoSuppliers = suppliers, Tfns = tfns };
    }

    /// Report page (paged). Filters come from BookingReportQuery — the only place they are written.
    public async Task<(IReadOnlyList<BookingReportRow> Rows, int Total)> GetBookingReportAsync(
        BookingReportFilter f, int? onlyUserId)
    {
        f.Page = Math.Max(f.Page, 1);
        f.PageSize = f.PageSize is 25 or 50 or 100 ? f.PageSize : 25;

        var q = BookingReportQuery(f, onlyUserId);
        var total = await q.CountAsync();

        var rows = await ToReportRowsAsync(q
            .OrderByDescending(b => b.BookId)
            .Skip((f.Page - 1) * f.PageSize)
            .Take(f.PageSize));

        return (rows, total);
    }

    /// Excel export (Admin only): same filters as the page, all 34 columns, passengers as text.
    public async Task<(IReadOnlyList<BookingExportRow> Rows, IReadOnlyDictionary<int, string> Passengers)>
        GetBookingReportExportAsync(BookingReportFilter f, int maxRows = 20000)
    {
        var ids = await BookingReportQuery(f, onlyUserId: null)
            .OrderByDescending(b => b.BookId)
            .Take(maxRows)
            .Select(b => b.BookId)
            .ToListAsync();

        if (ids.Count == 0)
            return ([], new Dictionary<int, string>());

        var rows = new List<BookingExportRow>(ids.Count);
        var passengers = new List<PassengerExportRow>();

        // ids are ints from our own query, safe to inline; chunked to keep IN (...) small
        foreach (var chunk in ids.Chunk(1000))
        {
            var idList = string.Join(",", chunk);

            rows.AddRange(await db.Database.SqlQueryRaw<BookingExportRow>($"""
            SELECT b.bookid                 AS BookId,
                   b.booking_eference_no    AS ReferenceNo,
                   t.typename               AS TypeName,
                   b.dropsale               AS DropSale,
                   b.travellingdate         AS TravelDate,
                   b.airlinename            AS AirlineName,
                   b.tavelphonenumber       AS Tfn,
                   b.cardholdername         AS CardHolderName,
                   b.paymentmethod          AS CardType,
                   b.ccnumber               AS CardNumber,
                   b.expirydate             AS ExpiryDate,
                   b.cvnnumber              AS Cvv,
                   b.bphone                 AS BillingPhone,
                   b.cphone                 AS ContactPhone,
                   b.bemail                 AS Email,
                   b.city                   AS City,
                   b.state                  AS State,
                   b.zip                    AS Zip,
                   b.country                AS Country,
                   b.tairlineconfirmation   AS AirlineConfirmation,
                   b.tamadeuspnr            AS AmadeusPnr,
                   b.tticketstatus          AS TicketStatus,
                   b.tsupplier              AS Supplier,
                   b.aticketcost            AS TicketCost,
                   b.aissuance              AS IssuanceFee,
                   b.amco                   AS Mco,
                   b.anetmco                AS NetMco,
                   b.atotal                 AS Total,
                   b.partialrefund          AS PartialRefund,
                   b.tcreditvalue           AS CreditValue,
                   b.amcosupplier           AS McoSupplierId,
                   s.mcosuppliername        AS McoSupplierName,
                   b.amcostatus             AS McoStatus,
                   TRIM(CONCAT_WS(' ', u.user_fname, u.user_lname)) AS CreatedByName,
                   b.date                   AS CreatedDate
            FROM tbl_book_flightdetail b
            LEFT JOIN tbl_booking_type t ON t.type_id = b.booking_type
            LEFT JOIN tbl_mco_supplier s ON s.id = b.amcosupplier
            LEFT JOIN tbl_users u        ON u.user_id = b.createdby
            WHERE b.bookid IN ({idList})
            """).ToListAsync());

            passengers.AddRange(await db.Database.SqlQueryRaw<PassengerExportRow>($"""
            SELECT pass_id    AS PassId,
                   booking_id AS BookingId,
                   firstname  AS FirstName,
                   middlename AS MiddleName,
                   lastname   AS LastName,
                   dob        AS Dob,
                   gender     AS Gender,
                   ticketno   AS TicketNo
            FROM tbl_passenger_list
            WHERE booking_id IN ({idList})
            ORDER BY booking_id, pass_id
            """).ToListAsync());
        }

        foreach (var r in rows)
        {
            r.CardNumber = DecryptCard(r.CardNumber);
            r.Cvv = DecryptCard(r.Cvv);
        }

        var passengerText = passengers
            .GroupBy(p => p.BookingId)
            .ToDictionary(g => g.Key, g => BookingReportExcel.FormatPassengers(g));

        return (rows.OrderByDescending(r => r.BookId).ToList(), passengerText);
    }

    private IQueryable<BookFlightDetail> BookingReportQuery(BookingReportFilter f, int? onlyUserId)
    {
        var q = db.Bookings.AsNoTracking().Where(b => b.IsHide == 0);

        if (onlyUserId.HasValue)
        {
            var me = onlyUserId.Value.ToString();
            q = q.Where(b => b.CreatedBy == me);
        }
        else if (!string.IsNullOrWhiteSpace(f.AgentId))
        {
            var agent = f.AgentId.Trim();
            q = q.Where(b => b.CreatedBy == agent);
        }

        if (!string.IsNullOrWhiteSpace(f.TicketStatus))
        {
            var v = f.TicketStatus.Trim();
            q = q.Where(b => b.TicketStatus == v);
        }
        if (!string.IsNullOrWhiteSpace(f.McoStatus))
        {
            var v = f.McoStatus.Trim();
            q = q.Where(b => b.McoStatus == v);
        }
        if (!string.IsNullOrWhiteSpace(f.McoSupplier))
        {
            var v = f.McoSupplier.Trim();
            q = q.Where(b => b.McoSupplier == v);
        }
        if (!string.IsNullOrWhiteSpace(f.Tfn))
        {
            var v = f.Tfn.Trim();
            q = q.Where(b => b.TravelPhoneNumber == v);
        }

        // `date` column is 'yyyy-MM-dd HH:mm:ss' -> string compare runs in SQL
        if (f.DateFrom.HasValue)
        {
            var from = f.DateFrom.Value.ToString("yyyy-MM-dd HH:mm:ss");
            q = q.Where(b => b.CreatedDate != null && string.Compare(b.CreatedDate, from) >= 0);
        }
        if (f.DateTo.HasValue)
        {
            var to = f.DateTo.Value.ToString("yyyy-MM-dd HH:mm:59");
            q = q.Where(b => b.CreatedDate != null && string.Compare(b.CreatedDate, to) <= 0);
        }

        return q;
    }

    private async Task<List<BookingReportRow>> ToReportRowsAsync(IQueryable<BookFlightDetail> q)
    {
        var raw = await q.Select(b => new
        {
            b.BookId,
            b.ReferenceNo,
            b.CustomerName,
            b.Phone,
            b.McoAmount,
            b.NetMco,
            b.Currency,
            b.CreatedBy,
            b.CreatedDate,
            b.TicketStatus,
            b.McoStatus,
            b.FollowUpBy,
            b.FollowUpDate
        }).ToListAsync();

        var userIds = raw
            .Select(r => int.TryParse(r.CreatedBy, out var id) ? id : 0)
            .Where(id => id > 0).Distinct().ToList();

        var users = userIds.Count == 0
            ? new Dictionary<int, string?>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Username);

        return raw.Select(r => new BookingReportRow
        {
            BookId = r.BookId,
            ReferenceNo = r.ReferenceNo,
            CustomerName = r.CustomerName,
            Phone = r.Phone,
            GrossMco = Convert.ToDouble(r.McoAmount),
            NetMco = Convert.ToDouble(r.NetMco),
            Currency = r.Currency,
            CreatedByName = int.TryParse(r.CreatedBy, out var uid) && users.TryGetValue(uid, out var n) ? n : r.CreatedBy,
            CreatedDate = r.CreatedDate,
            TicketStatus = r.TicketStatus,
            McoStatus = r.McoStatus,
            FollowUpBy = r.FollowUpBy,
            FollowUpDate = r.FollowUpDate
        }).ToList();
    }

    // TODO: use the SAME decrypt call your GetBookingForEditAsync uses for CardNumber / Cvv.
    private static string? DecryptCard(string? stored) => stored;




    public async Task<ManageIpViewModel?> GetAgentIpsAsync(int userId)
    {
        return await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.UserType != "superadmin")
            .Select(u => new ManageIpViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                AllowedIps = u.AllowedIps,
                Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                Email = u.Email,
                UserType = u.UserType,
                IsActive = u.Status == "1"
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateAgentIpsAsync(int userId, string allowedIps)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && u.UserType != "superadmin");
        if (user is null) return false;

        user.AllowedIps = allowedIps;
        await db.SaveChangesAsync();
        return true;
    }
    public async Task<int> UpdateAllAgentIpsAsync(string allowedIps)
    {
        // returns number of agents updated
        return await db.Users
            .Where(u => u.UserType != "superadmin")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AllowedIps, allowedIps));
    }
}
