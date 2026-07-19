namespace MonEcommerce.Application.Account.Models;

public record ProfileDto(string Name, string Email, List<AddressDto> Addresses);
