using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Infrastructure.ExternalServices;

public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorageService(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    public async Task<FileUploadResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string? folder = null,
        ImageTransformPreset preset = ImageTransformPreset.None,
        CancellationToken ct = default)
    {
        var transformation = new Transformation().FetchFormat("webp").Quality("auto");

        // AC #5: ratio 3:4, max width 1200px — a single crop=fill step with both an aspect ratio
        // and a width applies them together, rather than needing a second chained transformation.
        if (preset == ImageTransformPreset.ProductGallery)
        {
            transformation = transformation.AspectRatio("3:4").Crop("fill").Width(1200);
        }

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = folder ?? "mon-ecommerce",
            Transformation = transformation,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return new FileUploadResult(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        var deleteParams = new DeletionParams(publicId);
        await _cloudinary.DestroyAsync(deleteParams);
    }
}
