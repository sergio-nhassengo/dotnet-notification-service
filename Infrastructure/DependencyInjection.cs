using Application.Common.Interfaces;
using Application.Common.Security;
using Infrastructure.Extensions.Auth;
using Infrastructure.Extensions.Cors;
using Infrastructure.Extensions.Telemetry;
using Infrastructure.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Notifications.Interfaces;
using Application.Notifications.Retry;
using Confluent.Kafka;
using Infrastructure.Notifications;
using Infrastructure.Notifications.Health;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Providers;
using Infrastructure.Notifications.Workers;
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
        services.AddSingleton<IIntegrationEventSerializer, JsonIntegrationEventSerializer>();
        services.AddSingleton<EmailRetryPolicy>();
        services.AddScoped<IEmailTemplateRenderer, FileEmailTemplateRenderer>();
        services.AddSingleton<FakeEmailProvider>();
        services.AddHttpClient<BrevoEmailProvider>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<EmailProviderOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl); client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });
        services.AddScoped<IEmailProvider>(sp => sp.GetRequiredService<IOptions<EmailProviderOptions>>().Value.Provider.Equals("Brevo", StringComparison.OrdinalIgnoreCase)
            ? sp.GetRequiredService<BrevoEmailProvider>() : sp.GetRequiredService<FakeEmailProvider>());
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
            services.AddSingleton<IKafkaPublisher, KafkaPublisher>();
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
            services.AddHealthChecks().AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
        }
        services.AddHostedService<EmailDeliveryWorker>(); services.AddHostedService<OutboxCleanupWorker>();
        services.AddSingleton<NotificationMetrics>(sp => new NotificationMetrics(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IDateTime>()).Register());
        services.AddHostedService(sp => sp.GetRequiredService<NotificationMetrics>());
        services.AddHealthChecks().AddCheck<EmailProviderConfigurationHealthCheck>("email-provider", tags: ["ready"]);

        return services;
    }
}
