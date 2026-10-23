using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MySqlConnector;
using PreMailer.Net;
using RushMyBookings.Crm.Entities;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;
using System.Net;
using System.Net.Mail;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace RushMyBookings.Crm.Controllers;

public class BookingsController(ICrmDataService dataService, IBookingFileService fileService) : CrmControllerBase(dataService)
{
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = await BuildCreateViewModelAsync();
        return View(model);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBookingViewModel model, IFormFile[]? files)
    {
        model.BookingTypes = await DataService.GetBookingTypesAsync();


        
        if (CrmAccess.IsRestricted(User))
        {
            var existing = model.IsEdit ? await DataService.GetBookingStatusesAsync(model.BookId) : null;
            model.TicketStatus = BookingStatusHelper.AgentSafeTicket(model.TicketStatus, existing?.Ticket);
            model.McoStatus = BookingStatusHelper.AgentSafeMco(model.McoStatus, existing?.Mco);
        }
        BookingAmountCalculator.Apply(model);
        ModelState.Remove(nameof(model.TotalAmount));
        ModelState.Remove(nameof(model.NetMco));

        if (!ValidateBookingForm(model))
        {
            if (string.IsNullOrWhiteSpace(model.ReferenceNo))
            {
                model.ReferenceNo = await DataService.GenerateNextReferenceNoAsync();
            }

            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";

        try
        {
            var (bookId, referenceNo) = await DataService.CreateBookingAsync(model, userId);
            TempData["BookingCreated"] = referenceNo;

            await SaveAttachmentsAsync(bookId, files);

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql)
        {
            ModelState.AddModelError(string.Empty, $"Database error ({mysql.Number}): {mysql.Message}");
        }
        catch (MySqlException ex)
        {
            ModelState.AddModelError(string.Empty, $"Database error ({ex.Number}): {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(model.ReferenceNo))
        {
            model.ReferenceNo = await DataService.GenerateNextReferenceNoAsync();
        }

        return View(model);
    }


    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Create(CreateBookingViewModel model, IFormFile[]? files)
    //{
    //    model.BookingTypes = await DataService.GetBookingTypesAsync();

    //    if (!ValidateBookingForm(model))
    //    {
    //        if (string.IsNullOrWhiteSpace(model.ReferenceNo))
    //        {
    //            model.ReferenceNo = await DataService.GenerateNextReferenceNoAsync();
    //        }

    //        return View(model);
    //    }

    //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";

    //    try
    //    {
    //        var (bookId, referenceNo) = await DataService.CreateBookingAsync(model, userId);
    //        TempData["BookingCreated"] = referenceNo;
    //        await SaveAttachmentsAsync(bookId, files);

    //        return RedirectToAction(nameof(Index));
    //    }
    //    catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql)
    //    {
    //        ModelState.AddModelError(string.Empty, $"Database error ({mysql.Number}): {mysql.Message}");
    //    }
    //    catch (MySqlException ex)
    //    {
    //        ModelState.AddModelError(string.Empty, $"Database error ({ex.Number}): {ex.Message}");
    //    }

    //    if (string.IsNullOrWhiteSpace(model.ReferenceNo))
    //    {
    //        model.ReferenceNo = await DataService.GenerateNextReferenceNoAsync();
    //    }

    //    return View(model);
    //}



    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await CanAccessBookingAsync(id)) return RedirectToAction("Index", "Home");

        var model = await DataService.GetBookingForEditAsync(id);
        if (model is null) return NotFound();

        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CreateBookingViewModel model, IFormFile[]? files)
    {
        model.BookingTypes = await DataService.GetBookingTypesAsync();
        if (model.BookId is int bid && !await CanAccessBookingAsync(bid))
            return RedirectToAction("Index", "Home");

        BookingAmountCalculator.Apply(model);
        // asp-for reads ModelState first, so clear these or the old posted values show on redisplay
        ModelState.Remove(nameof(model.TotalAmount));
        ModelState.Remove(nameof(model.NetMco));

        if (CrmAccess.IsRestricted(User) && model.BookId is int editId)
        {
            var existing = model.IsEdit ? await DataService.GetBookingStatusesAsync(model.BookId) : null;
            model.TicketStatus = BookingStatusHelper.AgentSafeTicket(model.TicketStatus, existing?.Ticket);
            model.McoStatus = BookingStatusHelper.AgentSafeMco(model.McoStatus, existing?.Mco);
            var original = await DataService.GetBookingForEditAsync(editId);
            if (original is null) return NotFound();

            model.CardNumber = original.CardNumber;
            model.Email = original.Email;
            model.InboundPhone = original.InboundPhone;
            model.BillingPhone = original.BillingPhone;

            ModelState.Remove(nameof(model.CardNumber));
            ModelState.Remove(nameof(model.Email));
            ModelState.Remove(nameof(model.InboundPhone));
            ModelState.Remove(nameof(model.BillingPhone));
        }

        if (model.BookId is not > 0)
        {
            return NotFound();
        }

        if (!ValidateBookingForm(model))
        {
            return View("Create", model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";

        try
        {
            var updated = await DataService.UpdateBookingAsync(model, userId);
            if (!updated)
            {
                return NotFound();
            }
            await SaveAttachmentsAsync(model.BookId!.Value, files);

            TempData["BookingUpdated"] = model.ReferenceNo ?? "Booking";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql)
        {
            ModelState.AddModelError(string.Empty, $"Database error ({mysql.Number}): {mysql.Message}");
        }
        catch (MySqlException ex)
        {
            ModelState.AddModelError(string.Empty, $"Database error ({ex.Number}): {ex.Message}");
        }

        return View("Create", model);
    }
    private async Task SaveAttachmentsAsync(int bookingId, IFormFile[]? files)
    {
        if (files is null || files.Length == 0 || files.All(f => f.Length == 0))
        {
            return;
        }

        try
        {
            var count = await fileService.AddAttachmentsAsync(bookingId, files);
            if (count > 0)
            {
                TempData["UploadSuccess"] = $"{count} file(s) uploaded successfully.";
            }
        }
        catch (IOException ex)
        {
            TempData["UploadError"] = $"Booking saved, but files could not be uploaded: {ex.Message}";
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadAttachment(
        int id,
        IFormFile[] files,
        string? returnTo = null,
        int page = 1)
    {
        if (files.Length == 0 || files.All(f => f.Length == 0))
        {
            TempData["UploadError"] = "Please select at least one file to upload.";
            return RedirectAfterUpload(id, returnTo, page);
        }

        try
        {
            var count = await fileService.AddAttachmentsAsync(id, files);
            if (count == 0)
            {
                TempData["UploadError"] = "Upload failed. Booking not found or files were empty.";
            }
            else
            {
                TempData["UploadSuccess"] = $"{count} file(s) uploaded successfully.";
            }
        }
        catch (IOException ex)
        {
            TempData["UploadError"] = $"Could not save files: {ex.Message}";
        }

        return RedirectAfterUpload(id, returnTo, page);
    }

    private RedirectToActionResult RedirectAfterUpload(int id, string? returnTo, int page) =>
        string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Index), new { page });

    [HttpGet]
    public IActionResult DownloadAttachment(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !fileService.TryGetFilePath(file, out var path))
        {
            return NotFound();
        }

        var contentType = "application/octet-stream";
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg") contentType = "image/jpeg";
        else if (ext == ".png") contentType = "image/png";
        else if (ext == ".gif") contentType = "image/gif";
        else if (ext == ".pdf") contentType = "application/pdf";

        return PhysicalFile(path, contentType, Path.GetFileName(file));
    }

    private async Task<CreateBookingViewModel> BuildCreateViewModelAsync()
    {
       
        return new CreateBookingViewModel
        {
            ReferenceNo = await DataService.GenerateNextReferenceNoAsync(),
            BookingTypes = await DataService.GetBookingTypesAsync(),
            Tfns = await DataService.GetActiveTfnsAsync(),
            BookingType = "1",
            McoSuppliers = await DataService.GetMcoSuppliersAsync(),
            Passengers = [new PassengerFormModel()]
        };
    }

    private bool ValidateBookingForm(CreateBookingViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.BookingType))
        {
            ModelState.AddModelError(nameof(model.BookingType), "Booking type is required.");
        }

        var hasPassenger = model.Passengers.Any(p =>
            !string.IsNullOrWhiteSpace(p.FirstName) || !string.IsNullOrWhiteSpace(p.LastName));

        if (!hasPassenger)
        {
            ModelState.AddModelError(string.Empty, "At least one passenger with a name is required.");
        }

        return ModelState.IsValid;
    }

    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 25,
        string? phone = null,
        string? email = null,
        string? name = null,
        string? bookingNo = null,
        string? airlineConfirmation = null,
        string? lastFourCc = null,
        string? dateFrom = null,
        string? dateTo = null,
        string? type = null)
    {
        var filter = new BookingSearchFilter
        {
            Phone = phone,
            Email = email,
            Name = name,
            BookingNo = bookingNo,
            AirlineConfirmation = airlineConfirmation,
            LastFourCc = lastFourCc,
            DateFrom = NormalizeFilterDate(dateFrom),
            DateTo = NormalizeFilterDate(dateTo),
            Type = type
        };

        var bookingTypes = await DataService.GetBookingTypesAsync();
        var typeLookup = bookingTypes
            .Where(t => int.TryParse(t.Id, out _))
            .ToDictionary(t => int.Parse(t.Id!), t => t.Name ?? "Unknown");

        var bookings = await DataService.GetBookingsAsync(
    page, pageSize, filter, typeLookup, BookingAccessScope.For(User));

        var model = new BookingsIndexViewModel
        {
            Bookings = bookings,
            BookingTypes = bookingTypes,
            Filter = filter,
            Page = page
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await CanAccessBookingAsync(id)) return RedirectToAction("Index", "Home");

        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();

        return View(booking);
    }

    public async Task<IActionResult> Comments(
        int id,
        int page = 1,
        string? search = null,
        string? type = null)
    {
        var model = await DataService.GetBookingCommentsAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        model.ReturnPage = page;
        model.ReturnSearch = search;
        model.ReturnType = type;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        int id,
        string newComment,
        string returnTo = "comments",
        int returnPage = 1,
        string? returnSearch = null,
        string? returnType = null)
    {
        if (string.IsNullOrWhiteSpace(newComment))
        {
            TempData["CommentError"] = "Comment cannot be empty.";
            return RedirectAfterComment(id, returnTo, returnPage, returnSearch, returnType);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";

        try
        {
            var saved = await DataService.AddBookingCommentAsync(id, userId, newComment);
            if (!saved)
            {
                TempData["CommentError"] = "Could not save the comment. Check that the booking exists.";
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql)
        {
            TempData["CommentError"] = $"Database error ({mysql.Number}): {mysql.Message}";
        }
        catch (MySqlException ex)
        {
            TempData["CommentError"] = $"Database error ({ex.Number}): {ex.Message}";
        }

        return RedirectAfterComment(id, returnTo, returnPage, returnSearch, returnType);
    }

    private RedirectToActionResult RedirectAfterComment(
        int id,
        string returnTo,
        int returnPage,
        string? returnSearch,
        string? returnType)
    {
        if (string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToComments(id, returnPage, returnSearch, returnType);
    }

    private RedirectToActionResult RedirectToComments(
        int id,
        int returnPage,
        string? returnSearch,
        string? returnType)
    {
        return RedirectToAction(nameof(Comments), new
        {
            id,
            page = returnPage,
            search = returnSearch,
            type = returnType
        });
    }

    private static string? NormalizeFilterDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Replace('T', ' ');
    }



    public async Task<IActionResult> TFNManagement() =>
    View(await DataService.GetTfnsAsync());

    // Add (id null) aur Edit (id diya) dono ek hi form se
    [HttpGet]
    public async Task<IActionResult> TfnForm(int? id)
    {
        if (id is null) return View(new Tfn());

        var tfn = await DataService.GetTfnAsync(id.Value);
        return tfn is null ? NotFound() : View(tfn);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TfnForm(Tfn model)
    {
        if (!Tfn.Types.ContainsKey(model.Type))
            ModelState.AddModelError(nameof(model.Type), "Valid type select kariye");

        if (await DataService.TfnExistsAsync(model.TfnNumber, model.TfnId))
            ModelState.AddModelError(nameof(model.TfnNumber), "Ye TFN pehle se exist karta hai");

        if (!ModelState.IsValid) return View(model);

        var isNew = model.TfnId == 0;
        await DataService.SaveTfnAsync(model);

        TempData["Success"] = isNew ? "TFN successfully add ho gaya" : "TFN successfully update ho gaya";
        return RedirectToAction(nameof(TFNManagement));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TfnDelete(int id)
    {
        await DataService.DeleteTfnAsync(id);
        TempData["Success"] = "TFN delete ho gaya";
        return RedirectToAction(nameof(TFNManagement));
    }


    [HttpGet("~/Bookings/McoSupplierManagement")]
    public async Task<IActionResult> McoSupplierManagement() =>
    View(await DataService.GetMcoSupplierOptionsAsync());

    [HttpPost("~/Bookings/McoSupplierDelete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> McoSupplierDelete(int id)
    {
        await DataService.DeleteMcoSupplierAsync(id);
        TempData["Success"] = "MCO Supplier deleted.";
        return RedirectToAction(nameof(McoSupplierManagement));
    }

    [HttpPost("~/Bookings/McoSupplierToggle/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> McoSupplierToggle(int id)
    {
        await DataService.ToggleMcoSupplierStatusAsync(id);
        return RedirectToAction(nameof(McoSupplierManagement));
    }

    [HttpGet("~/Bookings/McoSupplierForm/{id:int}")]
    public async Task<IActionResult> McoSupplierForm(int id)
    {
        var model = await DataService.GetMcoSupplierFormAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("~/Bookings/McoSupplierForm/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> McoSupplierForm(int id, McoSupplierFormViewModel model)
    {
        model.Id = id;

        if (!ModelState.IsValid) return View(model);

        if (!await DataService.UpdateMcoSupplierAsync(model))
            return NotFound();

        TempData["Success"] = $"MCO Supplier '{model.Name}' updated.";
        return RedirectToAction(nameof(McoSupplierManagement));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadEditorImage(IFormFile? file, [FromServices] IWebHostEnvironment env)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file received." });

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".png";   // pasted screenshots often have no name

        if (!allowed.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only JPG, PNG, GIF or WEBP images are allowed." });

        // same folder the old PHP CRM uses
        var folder = Path.Combine(env.WebRootPath, "uploads", "junk");
        Directory.CreateDirectory(folder);

        var name = $"{Random.Shared.Next(100, 999)}_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, name)))
        {
            await file.CopyToAsync(stream);
        }

        // absolute URL so the image also loads in emails
        var url = $"{Request.Scheme}://{Request.Host}{Url.Content($"~/uploads/junk/{name}")}";
        return Json(new { url });
    }


    private bool FlightInfoFits(CreateBookingViewModel model)
    {
        var html = model.FlightPnr;
        if (string.IsNullOrEmpty(html)) return true;

        if (html.Contains("data:image", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.FlightPnr), "An image in Flight Information was not uploaded. Remove it and add it again.");
            return false;
        }
        if (System.Text.Encoding.UTF8.GetByteCount(html) > 65000)
        {
            ModelState.AddModelError(nameof(model.FlightPnr), "Flight Information is too large. Remove some text or images.");
            return false;
        }
        return true;
    }



    [HttpGet]
    public async Task<IActionResult> Email(int id)
    {
        ViewData["AckUrl"] = AckUrl(id);
        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        return View("EmailPreview", booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(
    int id,
    [FromServices] ICompositeViewEngine viewEngine,
    [FromServices] IConfiguration config)
    {
        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            return NotFound();
        }
        ViewData["AckUrl"] = AckUrl(id);
        // Customer email field is "comma separated"
        var recipients = (booking.Email ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => MailboxAddress.TryParse(e, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            TempData["EmailError"] = "No valid customer email on this booking. Add it in Billing Information → Email ID.";
            return RedirectToAction(nameof(Email), new { id });
        }

        if (!int.TryParse(booking.McoSupplier, out var supplierId))
        {
            TempData["EmailError"] = "Select \"MCO Supplier & Mail From\" on this booking before sending.";
            return RedirectToAction(nameof(Email), new { id });
        }

        var supplier = await DataService.GetMcoSupplierForMailAsync(supplierId);
        if (supplier is null ||
            string.IsNullOrWhiteSpace(supplier.FromEmail) ||
            string.IsNullOrWhiteSpace(supplier.FromSmtpPass))
        {
            TempData["EmailError"] = "The selected MCO supplier has no From email / SMTP password. Update it in MCO Supplier Management.";
            return RedirectToAction(nameof(Email), new { id });
        }

        // Same view as the preview, without the Send buttons
        ViewData["IsEmail"] = true;
        var html = await RenderViewToStringAsync(viewEngine, "EmailPreview", booking);
        html = PreMailer.Net.PreMailer.MoveCssInline(html, removeStyleElements: false).Html;

        var companyName = string.IsNullOrWhiteSpace(supplier.Name) ? "RushMyFlight" : supplier.Name.Trim();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(companyName, supplier.FromEmail.Trim()));
        foreach (var to in recipients)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }
        if (!string.IsNullOrWhiteSpace(supplier.Email) && MailboxAddress.TryParse(supplier.Email, out var replyTo))
        {
            message.ReplyTo.Add(replyTo);
        }
        message.Subject = string.IsNullOrWhiteSpace(booking.CustomEmailSubject)
            ? $"{companyName} - Booking {booking.ReferenceNo}"
            : booking.CustomEmailSubject.Trim();
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        var host = config["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;

        try
        {
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(host, port, SecureSocketOptions.Auto);   // 587 → STARTTLS, 465 → SSL
            await smtp.AuthenticateAsync(supplier.FromEmail.Trim(), supplier.FromSmtpPass.Replace(" ", ""));
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (MailKit.Security.AuthenticationException)
        {
            TempData["EmailError"] = $"SMTP login failed for {supplier.FromEmail}. Check the app password in MCO Supplier Management.";
            return RedirectToAction(nameof(Email), new { id });
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException ex)
        {
            TempData["EmailError"] = $"Email could not be sent: {ex.Message}";
            return RedirectToAction(nameof(Email), new { id });
        }

        TempData["EmailSuccess"] = $"Email sent to {string.Join(", ", recipients)} from {supplier.FromEmail}.";
        return RedirectToAction(nameof(Email), new { id });
    }

    private async Task<string> RenderViewToStringAsync(ICompositeViewEngine viewEngine, string viewName, object model)
    {
        var result = viewEngine.FindView(ControllerContext, viewName, isMainPage: true);
        if (!result.Success)
        {
            throw new InvalidOperationException($"View '{viewName}' was not found.");
        }

        ViewData.Model = model;
        using var writer = new StringWriter();
        var viewContext = new ViewContext(ControllerContext, result.View, ViewData, TempData, writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
    private string AckToken(int bookId)
    {
        var secret = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Acknowledge:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Acknowledge:Secret is missing in appsettings.json.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"ack:{bookId}"));
        return WebEncoders.Base64UrlEncode(hash)[..24];
    }

    private bool IsValidAckToken(int bookId, string? token) =>
        !string.IsNullOrEmpty(token) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(AckToken(bookId)), Encoding.ASCII.GetBytes(token));

    private string AckUrl(int bookId) =>
        Url.Action(nameof(Acknowledge), "Bookings", new { id = bookId, t = AckToken(bookId) }, Request.Scheme)!;

    private static readonly HttpClient GeoClient = new() { Timeout = TimeSpan.FromSeconds(3) };

    private string? ClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }

    // Same format as the old CRM: "Delhi, National Capital Territory of Delhi, India-110048"
    private static async Task<string?> LookupLocationAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        try
        {
            using var doc = JsonDocument.Parse(
                await GeoClient.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city,zip"));
            var r = doc.RootElement;
            if (r.GetProperty("status").GetString() != "success") return null;
            return $"{r.GetProperty("city").GetString()}, {r.GetProperty("regionName").GetString()}, " +
                   $"{r.GetProperty("country").GetString()}-{r.GetProperty("zip").GetString()}";
        }
        catch
        {
            return null;   // location is optional: never block the customer
        }
    }

    // ---------- Customer acknowledgement (public) ----------
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Acknowledge(int id, string? t)
    {
        if (!IsValidAckToken(id, t)) return NotFound();

        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();

        ViewData["AckToken"] = t;
        return View("Acknowledge", booking);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Acknowledge")]
    public async Task<IActionResult> AcknowledgeConfirm(int id, string? t)
    {
        if (!IsValidAckToken(id, t)) return NotFound();

        var ip = ClientIp();
        var location = await LookupLocationAsync(ip);
        if (!await DataService.AcknowledgeBookingAsync(id, ip, location)) return NotFound();

        return RedirectToAction(nameof(Acknowledge), new { id, t });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> AcknowledgeUpload(int id, string? t, IFormFile[]? files)
    {
        if (!IsValidAckToken(id, t)) return NotFound();

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };
        var valid = (files ?? []).Where(f => f.Length > 0).ToArray();

        if (valid.Length == 0)
        {
            TempData["AckUploadError"] = "Please choose at least one file.";
        }
        else if (valid.Length > 10)
        {
            TempData["AckUploadError"] = "You can upload up to 10 files at a time.";
        }
        else if (valid.Any(f => !allowed.Contains(Path.GetExtension(f.FileName))))
        {
            TempData["AckUploadError"] = "Only .jpg, .jpeg and .png files are allowed.";
        }
        else if (valid.Any(f => f.Length > 5 * 1024 * 1024))
        {
            TempData["AckUploadError"] = "Each file must be under 5 MB.";
        }
        else
        {
            await SaveAttachmentsAsync(id, valid);   // same storage as CRM attachments
            TempData["AckUploadSuccess"] = $"{valid.Length} file(s) uploaded successfully. Thank you!";
        }

        return RedirectToAction(nameof(Acknowledge), new { id, t });
    }


    [HttpGet]
    public async Task<IActionResult> ETicket(int id)
    {
        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        return View("ETicketPreview", booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendETicketEmail(
    int id,
    [FromServices] ICompositeViewEngine viewEngine,
    [FromServices] IConfiguration config)
    {
        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        // Customer email field is "comma separated"
        var recipients = (booking.Email ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => MailboxAddress.TryParse(e, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            TempData["ETicketError"] = "No valid customer email on this booking. Add it in Billing Information → Email ID.";
            return RedirectToAction(nameof(ETicket), new { id });
        }

        if (!int.TryParse(booking.McoSupplier, out var supplierId))
        {
            TempData["ETicketError"] = "Select \"MCO Supplier & Mail From\" on this booking before sending.";
            return RedirectToAction(nameof(ETicket), new { id });
        }

        var supplier = await DataService.GetMcoSupplierForMailAsync(supplierId);
        if (supplier is null ||
            string.IsNullOrWhiteSpace(supplier.FromEmail) ||
            string.IsNullOrWhiteSpace(supplier.FromSmtpPass))
        {
            TempData["ETicketError"] = "The selected MCO supplier has no From email / SMTP password. Update it in MCO Supplier Management.";
            return RedirectToAction(nameof(ETicket), new { id });
        }

        // Same view as the e-ticket preview, without the Send buttons
        ViewData["IsEmail"] = true;
        var html = await RenderViewToStringAsync(viewEngine, "ETicketPreview", booking);
        html = PreMailer.Net.PreMailer.MoveCssInline(html, removeStyleElements: false).Html;

        var companyName = string.IsNullOrWhiteSpace(supplier.Name) ? "RushMyFlight" : supplier.Name.Trim();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(companyName, supplier.FromEmail.Trim()));
        foreach (var to in recipients)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }
        if (!string.IsNullOrWhiteSpace(supplier.Email) && MailboxAddress.TryParse(supplier.Email, out var replyTo))
        {
            message.ReplyTo.Add(replyTo);
        }
        message.Subject = $"{companyName} - E-Ticket Confirmation - {booking.ReferenceNo}";
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        var host = config["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;

        try
        {
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(host, port, SecureSocketOptions.Auto);
            await smtp.AuthenticateAsync(supplier.FromEmail.Trim(), supplier.FromSmtpPass.Replace(" ", ""));
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (MailKit.Security.AuthenticationException)
        {
            TempData["ETicketError"] = $"SMTP login failed for {supplier.FromEmail}. Check the app password in MCO Supplier Management.";
            return RedirectToAction(nameof(ETicket), new { id });
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException ex) when (ex.Message.Contains("5.7.8"))
        {
            TempData["ETicketError"] = $"Gmail rejected the login for {supplier.FromEmail}. Create a new App Password for this account and update it in MCO Supplier Management.";
            return RedirectToAction(nameof(ETicket), new { id });
        }
        catch (Exception ex)
        {
            TempData["ETicketError"] = $"E-Ticket email could not be sent: {ex.Message}";
            return RedirectToAction(nameof(ETicket), new { id });
        }

        TempData["ETicketSuccess"] = $"E-Ticket email sent to {string.Join(", ", recipients)} from {supplier.FromEmail}.";
        return RedirectToAction(nameof(ETicket), new { id });
    }



    [HttpGet("~/Comments/Latest")]
    public async Task<IActionResult> Latest()
    {
        int? scope = !CrmAccess.IsRestricted(User)
            ? null
            : int.TryParse(CrmAccess.UserId(User), out var id) ? id : -1;

        var items = await DataService.GetLatestCommentsAsync(48, scope);
        return View(items);
    }

    [HttpGet("~/Bookings/ReadOnly/{id:int}")]
    public async Task<IActionResult> ReadOnly(int id)
    {
        var (exists, createdBy) = await DataService.GetBookingOwnerAsync(id);
        if (!exists) return NotFound();

        if (CrmAccess.IsRestricted(User) && createdBy != CrmAccess.UserId(User))
            return RedirectToAction("Index", "Home");

        var model = await DataService.GetBookingByIdAsync(id);   // <-- your existing details method
        if (model is null) return NotFound();

        ViewData["ReadOnly"] = true;
        return View("Details", model);                               // <-- your existing details view
    }


    private async Task<bool> CanAccessBookingAsync(int id)
    {
        if (!CrmAccess.IsRestricted(User)) return true;
        var (exists, createdBy) = await DataService.GetBookingOwnerAsync(id);
        return exists && createdBy == CrmAccess.UserId(User);
    }


}
