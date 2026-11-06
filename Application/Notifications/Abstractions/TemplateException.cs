namespace Application.Notifications.Abstractions;

/// <summary>Thrown by <see cref="IEmailTemplateRenderer"/> when a template or its variables are unusable.</summary>
public sealed class TemplateException(string message) : Exception(message);
