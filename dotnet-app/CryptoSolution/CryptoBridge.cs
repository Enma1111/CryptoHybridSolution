using Org.BouncyCastle.X509;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace CryptoSolution;

/// <summary>
/// Vermittelt zwischen der Rust-basierten ASN.1-Validierung und der .NET-Kryptographie.
/// Nutzt Caching zur Optimierung der Performance bei wiederholten Validierungen.
/// </summary>
/// <param name="cache">Die zu verwendende Cache-Implementierung (Dependency Injection).</param>
/// <param name="logger">Optionaler Logger für den Test-Output.</param>
public partial class CryptoBridge(ICache cache, ITestOutputHelper? logger = null)
{
    [LibraryImport("rust_core.dll", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool validate_x509_structure(IntPtr data, int len);

    /// <summary>
    /// Führt die Validierung und das Parsen des Zertifikats durch.
    /// Nutzt das Caching, um redundante Rust-Validierungen zu vermeiden.
    /// </summary>
    /// <param name="zertifikat">Das zu validierende Zertifikat als Byte-Array.</param>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn die ASN.1-Struktur ungültig ist.</exception>
    public async ValueTask RunIntegrationDemoAsync(ReadOnlyMemory<byte> certificate)
    {
        string certHash = Convert.ToBase64String(SHA256.HashData(certificate.Span));

        // GetOrAddAsync kapselt die gesamte Stampede-Protection und Cache-Logik
        bool isValid = await cache.GetOrAddAsync(certHash, async () =>
        {
            return ValidateX509Structure(certificate.Span);
        });

        if (!isValid)
        {
            throw new InvalidOperationException("Rust-Parser sagt: Ungültige ASN.1 Struktur!");
        }

        var parser = new X509CertificateParser();
        var cert = parser.ReadCertificate(certificate.ToArray());
        logger?.WriteLine($"Bouncy Castle bestätigt Zertifikat für: {cert.SubjectDN}");

    }

    public static bool ValidateX509Structure(ReadOnlySpan<byte> certificate)
    {
        unsafe
        {
            fixed (byte* ptr = certificate)
            {
                return validate_x509_structure((IntPtr)ptr, certificate.Length);
            }
        }
    }
}
