using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(Stream fileStream, string fileName, string? folder = null, CancellationToken ct = default);
    Task DeleteAsync(string publicId, CancellationToken ct = default);
}
