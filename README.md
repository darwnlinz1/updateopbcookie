# OpenBullet Cookie Edition

A modified fork of **OpenBullet** — a web testing suite with modular architecture built on **.NET Framework 4.7.1 / WPF**.

This edition introduces **cookie-based authentication testing**, **TLS Client Impersonation** (via `tls-client` native library), **binary cookie parsing** (Netscape, JSON, SQLite formats), and several engine-level performance improvements.

---

## Architecture Overview

```
OpenBullet.sln
├── RuriLib/                    # Core engine library
│   ├── Blocks/                 # Modular block system (request, parse, keycheck, etc.)
│   ├── Functions/              # Utility functions (crypto, encoding, parsing, HTTP)
│   ├── LoliScript/             # Script engine (LoliScript DSL interpreter)
│   ├── Models/                 # Data models (BotData, CData, CProxy, Cookie)
│   ├── Runner/                 # Multi-threaded runner engine (RunnerViewModel)
│   └── ViewModels/             # Settings and configuration view models
├── OpenBullet/                 # WPF desktop application (UI)
│   ├── Views/                  # XAML views (Runner, Stacker, Settings, Tools)
│   ├── ViewModels/             # UI-specific view models
│   └── Repositories/           # LiteDB data access layer
├── OpenBulletCLI/              # Command-line interface
└── PluginFramework/            # Plugin system for extensibility
```

---

## Key Components

### Block System (`RuriLib/Blocks/`)

The engine uses a modular **Block** architecture. Each block performs a specific operation and can be chained together in configs:

| Block | File | Purpose |
|-------|------|---------|
| `BlockRequest` | `BlockRequest.cs` | Standard HTTP requests via `HttpWebRequest` / Extreme.Net |
| `BlockTlsRequest` | `BlockTlsRequest.cs` | TLS-impersonated HTTP requests via native `tls-client` library |
| `BlockParse` | `BlockParse.cs` | Response parsing (LR, JSON, CSS, Regex) |
| `BlockKeycheck` | `BlockKeycheck.cs` | Status determination via keychain pattern matching |
| `BlockFunction` | `BlockFunction.cs` | String manipulation, crypto, encoding, GUID generation |
| `BlockCookieContainer` | `BlockCookieContainer.cs` | Cookie file loading (Netscape/JSON/SQLite formats) |
| `BlockBinaryCodec` | `BlockBinaryCodec.cs` | Binary data encoding/decoding operations |
| `BlockBypassCF` | `BlockBypassCF.cs` | Cloudflare challenge handling via Selenium |

### TLS Client Impersonation (`RuriLib/Functions/Requests/TlsClient/`)

Uses a native Go library (`tls-client-windows-64.dll`) to perform HTTP requests with browser-accurate TLS fingerprints:

- **`TlsClientNative.cs`** — P/Invoke bindings to the Go native library
- **`TlsClientRequest.cs`** / **`TlsClientResponse.cs`** — Request/response models
- Supports session management, cookie jars, proxy routing, and HTTP/2
- Session lifecycle: `Request()` → process → `DestroySession()` to prevent connection pool leaks

### Script Engine (`RuriLib/LoliScript/`)

**LoliScript** is a domain-specific scripting language that defines config logic:

- **`LoliScript.cs`** — Main interpreter with line-by-line execution
- **`BlockParser.cs`** — Parses script lines into Block objects
- **`CommandParser.cs`** — Handles flow control commands (IF/ELSE, JUMP, SET)
- Supports variable interpolation (`<varName>`), capture variables, and conditional branching

### Runner Engine (`RuriLib/Runner/`)

The multi-threaded execution engine:

- **`RunnerViewModel.cs`** — Core runner with `BackgroundWorker`-based parallelism
- **`RunnerBotViewModel.cs`** — Individual bot state management
- Features: proxy rotation, retry/ban loop evasion, safe timeout on completion wait, non-blocking UI updates via `Dispatcher.BeginInvoke`

### Data Models (`RuriLib/Models/`)

- **`BotData.cs`** — Per-bot execution context (status, variables, cookies, proxy)
- **`CData.cs`** — Input data line wrapper
- **`Cookie.cs`** — Cookie model with Netscape/JSON/SQLite parsing support
- **`CProxy.cs`** — Proxy model with status tracking

### Cookie Handling (`RuriLib/Models/Cookie.cs`)

Extended cookie support including:
- Netscape/Mozilla cookie format parsing
- JSON cookie array parsing (browser export format)
- SQLite cookie database reading (Chromium `Cookies` DB)
- Cookie path resolution and domain filtering

---

## Building

### Prerequisites
- Visual Studio 2017+ or MSBuild 15+
- .NET Framework 4.7.1 SDK
- NuGet package restore

### Build Commands

```bash
# Restore NuGet packages
nuget restore OpenBullet.sln

# Build Release
msbuild OpenBullet.sln /p:Configuration=Release /p:Platform="Any CPU"
```

The Roslyn compiler (`Microsoft.Net.Compilers 2.10.0`) is included via NuGet for consistent builds.

### Output
- `OpenBullet/bin/Release/OpenBulletCE.exe` — Main application
- `RuriLib/bin/Release/RuriLib.dll` — Core engine library

---

## Runtime Dependencies

The following native libraries are required at runtime (place in the executable directory):

| File | Purpose |
|------|---------|
| `tls-client-windows-64.dll` | TLS fingerprint impersonation (Go native) |
| `chromedriver.exe` | Selenium WebDriver for browser automation |
| `geckodriver.exe` | Firefox WebDriver support |

---

## Configuration System

Configs are stored as `.loli` (LoliScript) or `.anom` (legacy) files with a JSON settings header:

```
[SETTINGS]
{ "Name": "...", "NeedsProxies": false, ... }

[SCRIPT]
FUNCTION GenerateGUID -> VAR "sessId"
TLSREQUEST GET "https://example.com/api/data"
  COOKIE "<COK>"
  HEADER "Origin: https://example.com"
KEYCHECK BanOnToCheck=FALSE
  KEYCHAIN Success OR
    KEY "expected_data"
  KEYCHAIN Failure OR
    KEY "error"
PARSE "<SOURCE>" JSON "email" -> CAP "Email"
```

### KeyCheck Logic
- **Success** → Data marked as HIT
- **Failure** → Data marked as FAIL
- **Custom** → Data marked with custom status
- **Retry** → Data retried (re-queued)
- **Ban** → Proxy banned + data retried (governed by `BanLoopEvasion`)

---

## Database

Uses **LiteDB** (embedded NoSQL) for:
- Hit/result storage
- Proxy management
- Wordlist metadata
- Progress tracking

---

## Known Technical Details

### Engine Performance Fixes
1. **TLS Session Lifecycle**: Sessions are destroyed after each check cycle and on errors to prevent Go runtime connection pool leaks
2. **WPF Dispatcher**: Uses `BeginInvoke` (async) instead of `Invoke` (blocking) for UI updates to prevent deadlocks under high concurrency
3. **Safe Completion Timeout**: Bot completion wait loop has configurable timeout with forced abort for stuck workers

### HTTP Compression
`HttpCompression.cs` provides Brotli and GZip decompression for responses.

---

## License

This project is based on [OpenBullet](https://github.com/openbullet/openbullet) (GPLv3).
