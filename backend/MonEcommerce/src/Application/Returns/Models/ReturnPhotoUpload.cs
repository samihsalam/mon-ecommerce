namespace MonEcommerce.Application.Returns.Models;

// A plain Application-layer projection of an uploaded file — deliberately not ASP.NET Core's
// IFormFile, which the Application project has no reference to (same boundary-keeping reasoning
// already established for WebhookEvent/PaymentIntentResult in Payments).
public record ReturnPhotoUpload(Stream Content, string FileName);
