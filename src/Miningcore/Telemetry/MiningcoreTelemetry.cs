using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Miningcore.Telemetry;

public static class MiningcoreTelemetry
{
    public static readonly ActivitySource ActivitySource = new(
        "Miningcore",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0");

    public static readonly string ServiceName = "miningcore";
    public static readonly string ServiceVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

    /// <summary>
    /// Adds OpenTelemetry tracing to the service collection.
    /// Traces are exported via OTLP to a collector (Jaeger, Grafana Tempo, etc.)
    /// when OTEL_EXPORTER_OTLP_ENDPOINT is set, or disabled otherwise.
    /// </summary>
    public static IServiceCollection AddMiningcoreTelemetry(this IServiceCollection services)
    {
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if(string.IsNullOrEmpty(otlpEndpoint))
        {
            // No OTLP endpoint configured — trace locally only (zero overhead when not sampled)
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(ServiceName, serviceVersion: ServiceVersion))
                    .AddSource("Miningcore")
                    .SetSampler(new AlwaysOffSampler()));

            return services;
        }

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(ServiceName, serviceVersion: ServiceVersion))
                    .AddSource("Miningcore")
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });

        return services;
    }
}
