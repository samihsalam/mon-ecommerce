namespace MonEcommerce.Application.Common.Exceptions;

// Thrown when a return is requested for an order that isn't (yet, or anymore) eligible: not in
// OrderStatus.Delivered, or delivered more than 14 days ago (Story 5.1, AC #3 — explicitly a 422,
// not the 409 ConflictException already maps to; see Story 5.1's Dev Notes on why this needed its
// own type rather than reusing ConflictException).
public class ReturnWindowExpiredException : Exception
{
    public ReturnWindowExpiredException(string message) : base(message)
    {
    }
}
