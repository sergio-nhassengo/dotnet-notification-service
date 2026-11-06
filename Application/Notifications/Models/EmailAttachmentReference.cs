using Domain.Common;

namespace Application.Notifications.Models;

public sealed record EmailAttachmentReference(Guid AttachmentId, string? ContentId = null);
