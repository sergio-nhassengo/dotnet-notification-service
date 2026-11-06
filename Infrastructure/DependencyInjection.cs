using Application.Common.Interfaces;
using Application.Common.Retry;
using Application.Notifications.Abstractions;
using Application.Notifications.Contracts;
using Confluent.Kafka;
using Infrastructure.Notifications.Integration;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Providers;
using Infrastructure.Notifications.Templates;
using Infrastructure.Notifications.Workers;
using Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers notification infrastructure. The host must also register <see cref="INotificationCaller"/>,
    /// which identifies the calling application (the API resolves it from the authenticated request).
    /// </summary>
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<RetryPolicy>();

        services
            .AddNotificationOptions(configuration)
            .AddNotificationApplications()
            .AddEmailProviders(configuration)
            .AddKafka(configuration)
            .AddNotificationWorkers();

        return services;
    }

    private static IServiceCollection AddNotificationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaOptions>().Bind(configuration.GetSection(KafkaOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailDeliveryOptions>().Bind(configuration.GetSection(EmailDeliveryOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailProviderOptions>().Bind(configuration.GetSection(EmailProviderOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailProviderOptions>, EmailProviderOptionsValidator>();
        services.AddOptions<TemplateOptions>().Bind(configuration.GetSection(TemplateOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NotificationAttachmentOptions>().Bind(configuration.GetSection(NotificationAttachmentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NotificationIntegrationOptions>().Bind(configuration.GetSection("NotificationIntegration")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<NotificationIntegrationOptions>, NotificationIntegrationOptionsValidator>();
        return services;
    }

    private static IServiceCollection AddNotificationApplications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationDefaults, NotificationDefaults>();
        services.AddSingleton<IntegrationEventJsonSerializer>();
        services.AddSingleton<INotificationApplications, NotificationApplications>();
        services.AddScoped<IEmailPreflight, EmailSubmissionValidator>();
        services.AddScoped<FileEmailTemplateRenderer>();
        services.AddScoped<IEmailTemplateRenderer>(sp => sp.GetRequiredService<FileEmailTemplateRenderer>());
        return services;
    }

    private static IServiceCollection AddEmailProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Smtp"))
            .Validate(o => !string.Equals(configuration["EmailProvider:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(o.Host) && o.Port is >= 1 and <= 65535 && o.TimeoutSeconds is >= 1 and <= 120,
                "SMTP requires a host, valid port and a timeout of 1 to 120 seconds.").ValidateOnStart();
        services.AddOptions<SmtpRelayOptions>().Bind(configuration.GetSection("SmtpRelay"))
            .Validate(o => !string.Equals(configuration["EmailProvider:Provider"], "SmtpRelay", StringComparison.OrdinalIgnoreCase) || o.IsValid(),
                "SmtpRelay requires an HTTPS base URL, username, password, createdBy, token property and a timeout of 1 to 120 seconds.").ValidateOnStart();
        services.AddHttpClient<SmtpRelayEmailProvider>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<SmtpRelayOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<SmtpEmailProvider>();
        services.AddScoped<IEmailProvider>(sp => sp.GetRequiredService<IOptions<EmailProviderOptions>>().Value.Provider.ToLowerInvariant() switch
        {
            "smtprelay" => sp.GetRequiredService<SmtpRelayEmailProvider>(),
            "smtp" => sp.GetRequiredService<SmtpEmailProvider>(),
            _ => throw new InvalidOperationException("Unsupported email provider. Configure Smtp or SmtpRelay.")
        });
        return services;
    }

    // Kafka clients and the broker-facing workers are registered only when Kafka is enabled.
    private static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<KafkaEmailMessageProcessor>();
        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
        if (!kafka.Enabled)
            return services;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            SecurityProtocol = Enum.TryParse<SecurityProtocol>(kafka.SecurityProtocol, true, out var protocol) ? protocol : SecurityProtocol.Plaintext
        };
        services.AddSingleton<IProducer<string, string>>(_ => new ProducerBuilder<string, string>(producerConfig).Build());
        services.AddSingleton<KafkaPublisher>();
        services.AddSingleton<IConsumer<string, string>>(_ => new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = kafka.ConsumerGroup,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SecurityProtocol = producerConfig.SecurityProtocol
        }).Build());
        services.AddSingleton<IAdminClient>(_ => new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            SecurityProtocol = producerConfig.SecurityProtocol
        }).Build());
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<KafkaEmailConsumerWorker>();
        return services;
    }

    private static IServiceCollection AddNotificationWorkers(this IServiceCollection services)
    {
        services.AddHostedService<AttachmentCleanupWorker>();
        services.AddHostedService<EmailDeliveryWorker>();
        services.AddHostedService<OutboxCleanupWorker>();
        return services;
    }
}
