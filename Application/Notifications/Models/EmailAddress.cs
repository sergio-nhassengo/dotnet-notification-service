using Domain.Common;

namespace Application.Notifications.Models;

public sealed record EmailAddress(string Email, string? Name = null);
