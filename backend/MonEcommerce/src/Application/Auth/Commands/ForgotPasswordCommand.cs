using MediatR;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest<Result>;
