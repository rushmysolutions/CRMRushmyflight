using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;

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
    public async Task<IActionResult> Create(CreateBookingViewModel model)
    {
        model.BookingTypes = await DataService.GetBookingTypesAsync();

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
            var (_, referenceNo) = await DataService.CreateBookingAsync(model, userId);
            TempData["BookingCreated"] = referenceNo;
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

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await DataService.GetBookingForEditAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CreateBookingViewModel model)
    {
        model.BookingTypes = await DataService.GetBookingTypesAsync();

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
            BookingType = "1",
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

        var bookings = await DataService.GetBookingsAsync(page, pageSize, filter, typeLookup);

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
        var booking = await DataService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

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
}
