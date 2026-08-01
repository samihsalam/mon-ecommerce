namespace MonEcommerce.Application.Catalogue.Models;

// A plain Application-layer projection of an uploaded file — deliberately not ASP.NET Core's
// IFormFile, same boundary-keeping pattern as Returns.Models.ReturnPhotoUpload (Story 5.1).
public record ProductImageUpload(Stream Content, string FileName);
