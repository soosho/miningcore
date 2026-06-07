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
    ///
    /// Traces are exported via OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set.
    /// Sampling and other OTel env vars are honoured automatically when set.
    /// When no endpoint is set, AlwaysOffSampler guarantees zero overhead.
    ///
    /// Standard env vars:
    ///   OTEL_EXPORTER_OTLP_ENDPOINT   default http://localhost:4317
    ///   OTEL_TRACES_SAMPLER            parentbased_traceidratio
    ///   OTEL_TRACES_SAMPLER_ARG        0.1
    /// </summary>
    public static IServiceCollection AddMiningcoreTelemetry(this IServiceCollection services)
    {
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if(string.IsNullOrEmpty(otlpEndpoint))
        {
            // No OTLP endpoint — zero overhead AlwaysOffSampler
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
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });

        return services;
    }
}
