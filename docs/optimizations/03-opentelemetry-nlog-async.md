# Optimization: OpenTelemetry Tracing + NLog Async Wrappers

**Date**: June 2026  
**Area**: Logging & Telemetry (`Miningcore.Telemetry`, `Program.cs`)

## OpenTelemetry Distributed Tracing

### How it works

- A shared `ActivitySource("Miningcore")` is created statically in `MiningcoreTelemetry` 
- Every stratum request (`mining.subscribe`, `mining.authorize`, `mining.submit`) calls `ActivitySource.StartActivity("stratum.request")` with tags for method and connection ID
- `StartActivity` returns null immediately when no OTel SDK is registered — zero overhead, no allocations
- When OTel SDK IS registered, the created activity flows through the sampler and exporter pipeline

### How to enable

Set the env var before starting miningcore:

```bash
# gRPC (default protocol, recommend this)
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317

# Optionally configure sampling
export OTEL_TRACES_SAMPLER=parentbased_traceidratio
export OTEL_TRACES_SAMPLER_ARG=0.1

# Start miningcore
./Miningcore -c config.json
```

Traces flow: `stratum.request` → OTel exporter → OTLP collector → Jaeger/Grafana Tempo/etc.

### What's traced

| Operation | Tags | Frequency |
|-----------|------|-----------|
| `stratum.request` | method, connection_id | Every miner request |

More spans can be added by wrapping additional operations (share validation, RPC calls, payment processing) with `MiningcoreTelemetry.ActivitySource.StartActivity()`.

### Tested with

Jaeger all-in-one v1.57 via Docker:

```bash
docker run -d --name jaeger \
  -p 4317:4317 -p 16686:16686 \
  jaegertracing/all-in-one:1.57

OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 ./Miningcore -c config.json
```

Verified: `miningcore` service appears in Jaeger UI at `http://localhost:16686`, `stratum.request` spans visible with method and connection_id tags. Uses gRPC on port 4317 (OTel SDK default protocol).

### When disabled (no OTLP endpoint)

When `OTEL_EXPORTER_OTLP_ENDPOINT` is not set, `AlwaysOffSampler` is used. `ActivitySource.StartActivity()` returns null immediately — no activity object, no allocations, no export overhead. Suitable for production pools that don't need tracing.

## NLog Async Wrappers

### Why

With synchronous NLog file targets, a slow/flaky disk can cause threadpool starvation. Every stratum request that logs a debug message would block until the write completes. The async wrapper decouples logging from mining by queuing messages and flushing on a background thread.

### What changed

All `FileTarget` instances (main log, API log, per-pool logs) are automatically wrapped with `AsyncTargetWrapper` in `ConfigLogger.WrapFileTargetsWithAsync()`:

- **Queue size**: 10,000 messages — smooths disk I/O spikes
- **Overflow action**: `Discard` — never blocks a pool worker thread because of slow disk
- **Batch size**: 100 messages
- **Flush interval**: 200ms
- **Console targets**: left synchronous (negligible I/O, no benefit from async)

### Packages Added

| Package | Version | Purpose |
|---------|---------|---------|
| `OpenTelemetry` | 1.9.0 | Core tracing API + DI integration |
| `OpenTelemetry.Extensions.Hosting` | 1.9.0 | Hosted service lifecycle |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.9.0 | OTLP gRPC export to collectors |

### Benchmark Impact

Zero allocation overhead when OTel is disabled (no endpoint configured). When enabled, the sampling ratio determines overhead — at 10% sampling, only 1 in 10 requests creates a trace.

### Verification

```bash
# 1. Start Jaeger
docker run -d --name jaeger -p 4317:4317 -p 16686:16686 jaegertracing/all-in-one:1.57

# 2. Start miningcore with OTel
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 ./Miningcore -c config.json

# 3. Send a stratum request
echo '{"params": ["testminer1", "x"], "id": 1, "method": "mining.subscribe"}' | nc localhost 3032

# 4. Check traces in Jaeger UI
open http://localhost:16686
# Select service "miningcore" and search
```
