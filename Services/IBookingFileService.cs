namespace RushMyBookings.Crm.Services;

public interface IBookingFileService
{
    Task<int> AddAttachmentsAsync(int bookingId, IEnumerable<IFormFile> files, CancellationToken cancellationToken = default);
    bool TryGetFilePath(string storedFileName, out string fullPath);
    IReadOnlyList<string> ParseFileNames(string? attachFiles);
}
