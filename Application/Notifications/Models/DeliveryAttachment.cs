using Domain.Common;

namespace Application.Notifications.Models;

public sealed record DeliveryAttachment(string FileName, string ContentType, byte[] Content, string? ContentId);
