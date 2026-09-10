using Application.Notifications.Integration;
using Infrastructure.Notifications.Integration;
using Application.Common.Email;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.Common.Notifications;
using Application.Common.Retry;
using Application.Common.Security;
using Confluent.Kafka;
using Infrastructure.Extensions.Auth;
using Infrastructure.Extensions.Cors;
using Infrastructure.Extensions.Telemetry;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Providers;
using Infrastructure.Notifications.Workers;
using Infrastructure.Notifications;
using Infrastructure.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfraOpenTelemetry(configuration);
        services.AddInfraCors(configuration);
        services.AddInfraAuth(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddOptions<KafkaOptions>().Bind(configuration.GetSection(KafkaOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailDeliveryOptions>().Bind(configuration.GetSection(EmailDeliveryOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailProviderOptions>().Bind(configuration.GetSection(EmailProviderOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TemplateOptions>().Bind(configuration.GetSection(TemplateOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailProviderOptions>, EmailProviderOptionsValidator>();
        services.AddSingleton<INotificationDefaults, NotificationDefaults>();
        services.AddSingleton<IntegrationEventJsonSerializer>();
        services.AddSingleton<KafkaEmailMessageProcessor>();
        services.AddSingleton<RetryPolicy>();
        services.AddOptions<NotificationIntegrationOptions>().Bind(configuration.GetSection("NotificationIntegration"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NotificationIntegrationOptions>, NotificationIntegrationOptionsValidator>();
        services.AddSingleton<INotificationApplications, NotificationApplications>();
        services.AddScoped<INotificationCaller, NotificationCaller>();
        services.AddScoped<IEmailPreflight, EmailPreflight>();
        services.AddScoped<FileEmailTemplateRenderer>();
        services.AddScoped<IEmailTemplateRenderer>(sp => sp.GetRequiredService<FileEmailTemplateRenderer>());
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Smtp"))
            .Validate(o => !string.Equals(configuration["EmailProvider:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(o.Host) && o.Port is >= 1 and <= 65535 && o.TimeoutSeconds is >= 1 and <= 120,
                "SMTP requires a host, valid port and a timeout of 1 to 120 seconds.").ValidateOnStart();
        services.AddScoped<SmtpEmailProvider>();
        services.AddSingleton<FakeEmailProvider>();
        services.AddHttpClient<BrevoEmailProvider>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<EmailProviderOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl); client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });
        services.AddScoped<IEmailProvider>(sp => sp.GetRequiredService<IOptions<EmailProviderOptions>>().Value.Provider.ToLowerInvariant() switch
        {
            "brevo" => sp.GetRequiredService<BrevoEmailProvider>(),
            "smtp" => sp.GetRequiredService<SmtpEmailProvider>(),
            _ => sp.GetRequiredService<FakeEmailProvider>()
        });
        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
        if (kafka.Enabled)
        {
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
            services.AddSingleton<IAdminClient>(_ => new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafka.BootstrapServers, SecurityProtocol = producerConfig.SecurityProtocol }).Build());
            services.AddHostedService<OutboxPublisherWorker>();
            services.AddHostedService<KafkaEmailConsumerWorker>();
        }
        services.AddHostedService<EmailDeliveryWorker>(); services.AddHostedService<OutboxCleanupWorker>();

        return services;
    }
}
