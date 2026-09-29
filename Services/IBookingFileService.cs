namespace RushMyBookings.Crm.Services;

public interface IBookingFileService
{
    Task<int> AddAttachmentsAsync(int bookingId, IEnumerable<IFormFile> files);
    bool TryGetFilePath(string storedFileName, out string fullPath);
    IReadOnlyList<string> ParseFileNames(string? attachFiles);
}
