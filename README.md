# OpenBullet Cookie Edition — Authorized Authentication Testing Framework

> **Scope**: This project is developed and maintained for **authorized penetration testing, QA automation, and authentication endpoint validation** on systems the operator owns or has explicit written permission to test. All usage must comply with applicable laws and service terms.

---

## Overview

OpenBullet Cookie Edition is a **WPF-based automation framework** for testing web application authentication endpoints, session handling, cookie lifecycle management, and API authentication flows. It is built on top of the original OpenBullet project and extended with:

- **TLS Client Impersonation** — Browser-accurate TLS fingerprinting via `tls-client` (Go native library) for testing endpoints that perform fingerprint-based bot detection
- **Binary Cookie Parsing** — Chromium SQLite, Netscape, and JSON cookie format support for session replay testing
- **Modular Block Architecture** — Script-based automation blocks for building test flows: HTTP requests, response parsing, key-based status classification, conditional branching
- **Concurrent Execution Engine** — Multi-threaded runner with configurable parallelism, retry policies, proxy routing, and safe timeout/abort lifecycle management

**Primary use cases**:
- Validating authentication session lifecycle (login, cookie expiry, forced logout)
- Testing whether your service correctly detects and rejects replayed session cookies
- Benchmarking login endpoint throughput and error handling under load
- Validating TLS fingerprint-based bot detection from the client perspective
- Reproducing specific browser TLS handshake profiles in automation

---

## Architecture Overview

```
OpenBullet.sln
├── RuriLib/                    # Core engine library
│   ├── Blocks/                 # Modular block system
│   │   ├── BlockRequest.cs         Standard HTTP via HttpWebRequest / Extreme.Net
│   │   ├── BlockTlsRequest.cs      TLS-impersonated HTTP (browser fingerprint)
│   │   ├── BlockParse.cs           Response parsing (JSON, LR, CSS, Regex)
│   │   ├── BlockKeycheck.cs        Status classification (pass/fail/retry/ban patterns)
│   │   ├── BlockFunction.cs        String, crypto, encoding, GUID utilities
│   │   ├── BlockCookieContainer.cs Cookie loading (Netscape / JSON / SQLite)
│   │   ├── BlockBinaryCodec.cs     Binary data encode/decode
│   │   └── BlockBypassCF.cs        Cloudflare challenge handling (Selenium)
│   ├── Functions/              # Utility function modules
│   │   ├── Requests/               HTTP client + TLS client wrappers
│   │   │   └── TlsClient/          Native Go tls-client P/Invoke bindings
│   │   ├── Crypto/                 Hashing and cryptographic utilities
│   │   ├── Parsing/                HTML, JSON, regex parsing helpers
│   │   └── Conditions/             Key matching / condition evaluation
│   ├── LoliScript/             # Test script DSL engine
│   │   ├── LoliScript.cs           Line-by-line interpreter
│   │   ├── Parser/BlockParser.cs   Block instruction parser
│   │   └── Parser/CommandParser.cs Flow control (IF/ELSE, JUMP, SET)
│   ├── Models/                 # Core data models
│   │   ├── BotData.cs              Per-worker execution context
│   │   ├── Cookie.cs               Cookie model + multi-format parser
│   │   └── CData.cs / CProxy.cs    Input data + proxy models
│   ├── Runner/                 # Concurrent execution engine
│   │   ├── RunnerViewModel.cs      Master runner (BackgroundWorker parallelism)
│   │   └── RunnerBotViewModel.cs   Individual worker state machine
│   └── ViewModels/             # Settings and configuration
├── OpenBullet/                 # WPF desktop application
│   ├── Views/                  # XAML views (Runner, Stacker, Settings, Tools)
│   ├── ViewModels/             # UI state management
│   └── Repositories/           # LiteDB data access layer
├── OpenBulletCLI/              # Command-line interface
└── PluginFramework/            # Plugin extensibility system
```

---

## Block System

Test scripts (LoliScript `.loli` files) are composed of blocks — each block performs one operation:

| Block | Purpose |
|-------|---------|
| `TLSREQUEST` | Send HTTP request with specific TLS fingerprint (browser impersonation) |
| `REQUEST` | Standard HTTP request |
| `KEYCHECK` | Classify response outcome via pattern matching |
| `PARSE` | Extract values from response (JSON path, LR, CSS, Regex) |
| `FUNCTION` | Transform strings (Base64, HMAC, GUID, Replace, etc.) |
| `COOKIECONTAINER` | Load browser cookie files (Chromium SQLite, Netscape, JSON) |
| `BINARYCODEC` | Encode/decode binary protocol buffers or custom formats |
| `BYPASSCF` | Obtain Cloudflare clearance via Selenium for subsequent requests |

### KeyCheck Classification

The `KEYCHECK` block classifies each test run's response into one of:

| Status | Meaning |
|--------|---------|
| **Success** | Response matches success criteria (e.g., authenticated session active) |
| **Failure** | Response matches failure criteria (e.g., session invalid/expired) |
| **Custom** | Response matches a user-defined named status |
| **Retry** | Transient error — retry the same input |
| **Ban** | Rate-limit or IP block detected — rotate proxy, retry |

---

## TLS Client Impersonation

`RuriLib/Functions/Requests/TlsClient/` wraps the [tls-client](https://github.com/bogdanfinn/tls-client) Go native library via P/Invoke. This enables:

- Browser-accurate TLS ClientHello fingerprints (Chrome, Firefox, Safari, Edge profiles)
- HTTP/2 + ALPN negotiation matching browser behavior
- Session lifecycle management — `Request()` → process → `DestroySession()` to prevent stale connection pool leaks

This is particularly useful for **testing whether your service's fingerprint-based detection** correctly identifies automated requests vs. legitimate browser sessions.

---

## Cookie Session Testing

`RuriLib/Models/Cookie.cs` + `BlockCookieContainer.cs` enable:

- Loading exported browser cookies (Chromium `Cookies` SQLite DB, Netscape format, JSON array export)
- Filtering by domain prefix for scoped session replay
- Validating whether your service correctly rejects expired, replayed, or forged session cookies

---

## LoliScript Example

A minimal test script that validates an API authentication endpoint:

```loliscript
[SETTINGS]
{ "Name": "auth-session-test", "NeedsProxies": false, "SuggestedBots": 10 }

[SCRIPT]
# Load session cookie from file
COOKIECONTAINER "example." "<COOKIEPATH>" -> SAVE "SessionCookie"

# Generate request identifiers
FUNCTION GenerateGUID -> VAR "requestId"

# Test authentication endpoint
TLSREQUEST GET "https://api.example.com/v1/me"
  COOKIE "<SessionCookie>"
  HEADER "Accept: application/json"
  HEADER "x-request-id: <requestId>"

# Classify response
KEYCHECK BanOnToCheck=FALSE
  KEYCHAIN Success OR
    KEY "\"authenticated\":true"
  KEYCHAIN Failure OR
    KEY "\"error\":"
    KEY "<RESPONSECODE>" Contains "401"
    KEY "<RESPONSECODE>" Contains "403"
  KEYCHAIN Retry OR
    KEY "<RESPONSECODE>" Contains "429"
    KEY "<RESPONSECODE>" Contains "503"

# Extract and capture data from successful response
PARSE "<SOURCE>" JSON "userId" -> CAP "UserId"
PARSE "<SOURCE>" JSON "email" -> CAP "Email"
```

---

## Runner Engine Technical Notes

### Concurrency & Lifecycle

- Workers are `BackgroundWorker`-based with safe abort via `CancellationToken`
- Bot completion loop has configurable timeout: `maxWaitSeconds = Max(30, RequestTimeout × 2)`
- On timeout, stuck workers are safely aborted to prevent indefinite hang on stale TLS sockets

### WPF UI Thread Safety

All UI updates use `Dispatcher.BeginInvoke(action, DispatcherPriority.Normal)` (async, non-blocking). This prevents worker thread deadlocks under high concurrency (2,000–3,000 requests/min).

### TLS Session Memory Management

`TlsClientNative.DestroySession(sessionId)` is called in both:
- `finally` block after each check cycle (normal cleanup)
- `catch` block on error/timeout (immediate cleanup of stale Go runtime connection pools)

This prevents memory/connection accumulation in the Go native runtime when servers close TCP Keep-Alive connections.

---

## Building

### Prerequisites

- Visual Studio 2017+ or MSBuild 15+
- .NET Framework 4.7.1 SDK
- NuGet (package restore)

### Build

```bash
# Restore NuGet packages
nuget restore OpenBullet.sln

# Build Release
msbuild OpenBullet.sln /p:Configuration=Release /p:Platform="Any CPU"
```

Roslyn compiler (`Microsoft.Net.Compilers 2.10.0`) is referenced via NuGet for consistent cross-machine builds.

### Output

```
OpenBullet/bin/Release/OpenBulletCE.exe     Main WPF application
RuriLib/bin/Release/RuriLib.dll             Core engine library
```

### Runtime Dependencies

Place these in the same directory as `OpenBulletCE.exe`:

| File | Source |
|------|--------|
| `tls-client-windows-64.dll` | [bogdanfinn/tls-client releases](https://github.com/bogdanfinn/tls-client) |
| `chromedriver.exe` | [ChromeDriver downloads](https://chromedriver.chromium.org/) |
| `geckodriver.exe` | [Mozilla geckodriver releases](https://github.com/mozilla/geckodriver) |

---

## Database

LiteDB (embedded NoSQL) manages:
- Test run result records (hits, failures, to-check)
- Proxy pool state and ban tracking
- Wordlist/input dataset metadata
- Session progress checkpointing

---

## Known Issues & Technical Decisions

### Why `ShouldTriggerEvasion` Respects `BanLoopEvasion = 0`

Setting `BanLoopEvasion = 0` in Proxy Settings means "disabled" (retry without limit). A previous modification hardcoded this to 3, breaking the expected behavior. Current implementation follows the original OpenBullet contract: `return retries < evasionValue || evasionValue == 0`.

### Why `Dispatcher.BeginInvoke` Not `Invoke`

Under concurrent load, synchronous `Dispatcher.Invoke` from 80+ workers caused the WPF UI thread queue to fill, deadlocking all workers. `BeginInvoke` with `DispatcherPriority.Normal` queues asynchronously, allowing the UI thread to batch process updates.

---

## License

Based on [OpenBullet](https://github.com/openbullet/openbullet) — GNU General Public License v3.0.

**This software is provided for authorized testing purposes only. The maintainers accept no responsibility for misuse.**
