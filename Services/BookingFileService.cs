using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities;

namespace RushMyBookings.Crm.Services;

public sealed class BookingFileService(IWebHostEnvironment env, CrmDbContext db) : IBookingFileService
{
    private const string UploadSubPath = "uploads/bookings";

    private string UploadDirectory => Path.Combine(env.WebRootPath, UploadSubPath);

    public async Task<int> AddAttachmentsAsync(
        int bookingId,
        IEnumerable<IFormFile> files)
    {
        var validFiles = files.Where(f => f.Length > 0).ToList();
        if (validFiles.Count == 0)
        {
            return 0;
        }

        var exists = await db.Bookings.AsNoTracking()
            .AnyAsync(b => b.BookId == bookingId);

        if (!exists)
        {
            return 0;
        }

        Directory.CreateDirectory(UploadDirectory);

        var savedNames = new List<string>();
        foreach (var file in validFiles)
        {
            var random = Random.Shared.Next(1000, 9999);
            var safeName = Path.GetFileName(file.FileName);
            var storedName = $"{bookingId}_{random}rnd{safeName}";
            var path = Path.Combine(UploadDirectory, storedName);

            await using var stream = File.Create(path);
            await file.CopyToAsync(stream);
            savedNames.Add(storedName);
        }

        var appended = string.Concat(savedNames.Select(name => $"{name}##"));

        var attachment = await db.Attachments
            .Where(a => a.BookingId == bookingId)
            .OrderByDescending(a => a.AttachId)
            .FirstOrDefaultAsync();

        if (attachment is null)
        {
            db.Attachments.Add(new BookAttachment
            {
                BookingId = bookingId,
                AttachFiles = appended,
                InitialUploadQuality = 0
            });
        }
        else
        {
            attachment.AttachFiles = (attachment.AttachFiles ?? string.Empty) + appended;
        }

        await db.SaveChangesAsync();
        return savedNames.Count;
    }

    public bool TryGetFilePath(string storedFileName, out string fullPath)
    {
        var safeName = Path.GetFileName(storedFileName);
        fullPath = Path.Combine(UploadDirectory, safeName);
        return File.Exists(fullPath);
    }

    public IReadOnlyList<string> ParseFileNames(string? attachFiles)
    {
        if (string.IsNullOrWhiteSpace(attachFiles))
        {
            return [];
        }

        return attachFiles
            .Split("##", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
