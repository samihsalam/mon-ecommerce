using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

// Deliberately no CloudinaryDotNet.Transformation type here — IFileStorageService is an
// Application-layer interface with no reference to any specific storage provider's SDK. This
// enum lets a caller ask for a named transform without leaking Infrastructure types across the
// boundary; CloudinaryFileStorageService decides what each preset actually means in Cloudinary
// terms.
public enum ImageTransformPreset
{
    None,
    // 3:4 crop, max width 1200px, WebP (Story 6.2, AC #5).
    ProductGallery,
}

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string? folder = null,
        ImageTransformPreset preset = ImageTransformPreset.None,
        CancellationToken ct = default);
    Task DeleteAsync(string publicId, CancellationToken ct = default);
}
