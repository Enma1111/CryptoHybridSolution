# High-Performance X.509 ASN.1 Validator (Hybrid .NET/Rust)

Dieses Projekt demonstriert eine hochperformante Hybrid-Architektur zur Validierung von X.509-Zertifikaten. Es kombiniert die Speichersicherheit und Performance von **Rust** (für das Low-Level Parsing) mit der Flexibilität und Enterprise-Architektur von **.NET**.

## Motivation
Die Validierung von Zertifikatsstrukturen kann bei hoher Last rechenintensiv sein. Dieses System implementiert ein "Fast-Fail"-Prinzip:
1. **Rust-Core:** Validiert die ASN.1-Struktur extrem schnell via FFI, bevor die schwere kryptographische Logik in .NET (Bouncy Castle) aufgerufen wird.
2. **Caching:** Schützt das System vor "Cache Stampedes" durch eine thread-sichere `GetOrAddAsync`-Implementierung.
3. **Architektur:** Trennung von Infrastruktur (Cache) und Geschäftslogik durch Dependency Inversion.

## Technische Highlights

* **Rust (no_std):** Zero-Cost-Abstraktionen für ASN.1 DER/BER Parsing. Keine Heap-Allokationen während des Parsens.
* **Hybrid-Interoperabilität:** Nutzung von `[LibraryImport]` (P/Invoke) zur performanten Kommunikation zwischen .NET und Rust.
* **Memory-Optimierung:** Einsatz von `ValueTask` statt `Task` zur Vermeidung von Garbage-Collector-Druck bei häufigen Cache-Hits.
* **Concurrency-Management:** Thread-sicheres Locking mit `SemaphoreSlim` in einem `ConcurrentDictionary`, um redundante Validierungen bei hoher Last zu verhindern.
* **Design Patterns:** Einsatz von **Dependency Inversion** (`ICache`), um den Cache-Provider (Memory vs. Redis) austauschbar zu machen.

## Projektstruktur
- `/rust_core`: ASN.1 Parsing-Logik, `no_std`, FFI-Schnittstelle.
- `/src`: .NET Bridge (`CryptoBridge`), Caching-Adapter und Business-Logik.
- `/tests`: Integrationstests inkl. `FakeCache`-Implementierung für sauberes Testing.

## Architektur-Entscheidungen

### Warum Rust + .NET?
Rust bietet uns eine strikte Kontrolle über Speicher (`no_std`, `unsafe` mit klaren Verträgen), was ideal für die Validierung von rohen Binärdaten ist. .NET liefert uns ein exzellentes Ökosystem für kryptographische Standards (Bouncy Castle).

### Warum das Interface `ICache`?
Wir folgen dem **Dependency Inversion Principle**. Für diese Demo nutzen wir einen `MemoryCacheAdapter`. Der Austausch gegen einen `RedisCacheAdapter` erfordert in der `Program.cs` lediglich den Austausch einer einzigen Registrierung, ohne die Geschäftslogik zu verändern.

---

## Erste Schritte

### Voraussetzungen
- Rust (Toolchain)
- .NET 8.0+

### Build & Run
1. Baue den Rust-Core:
   ```bash
   cd rust_core
   cargo build --release

### Build-Automatisierung
Dieses Projekt verwendet eine `Directory.Build.targets` Datei, um den Entwicklungsprozess zu automatisieren:
- Sobald das .NET-Projekt gebaut wird, stellt ein MSBuild-Target sicher, dass die aktuelle Rust-Library (`.dll` oder `.so`) automatisch in das Ausgabeverzeichnis kopiert wird.
- Dadurch entfällt manuelles Dateimanagement, und die `CryptoBridge` findet die Library ohne zusätzliche Konfiguration.