# Miningcore Community Edition (MCCE)

MCCE is a modernized, actively maintained fork of [Miningcore](https://github.com/oliverw/miningcore) — the best open-source cryptocurrency mining pool server. The original project was **discontinued and archived in 2022**. This fork picks up where it left off: .NET 8, Ubuntu 24.04, performance improvements, and ongoing maintenance.

## What's Different From Original

| | Original Miningcore | MCCE |
|---|---|---|
| **Status** | Archived (2022) | Active |
| **.NET Version** | .NET 6 | .NET 8 |
| **Target OS** | Ubuntu 22.04 | Ubuntu 24.04 / Windows |
| **API Stack** | MVC Controllers | Minimal APIs (67× faster) |
| **Stratum Parsing** | Newtonsoft.Json | Utf8JsonReader (5.4× faster) |
| **Memory Alloc** | 7.1 KB/request | 0.73 KB/request |
| **Coin Support** | Same | All original coins + fixes for GCC 13 |

## Documentation

- [Full Changelog](changelog.md) — every change since the fork
- [Optimization: REST API Migration](optimizations/01-api-minimal-migration.md)
- [Optimization: Stratum JSON Parsing](optimizations/02-stratum-utf8reader.md)
- [Benchmark: Stratum Processing](benchmarks/stratum-comparison.md)
- [Benchmark: API Throughput](benchmarks/api-comparison.md)

## Quick Start

```bash
# Clone
git clone https://github.com/soosho/miningcore.git
cd miningcore

# Build (Linux)
./build-ubuntu-24.04.sh

# Run
cd src/Miningcore/bin/Release/net8.0
./Miningcore -c config.json
```

## Donations

BTC: `1AqqFf13RcfGbwa4GQGQV27T6HL1r35WVk`

## License

MIT — same as original Miningcore.
