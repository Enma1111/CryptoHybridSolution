using System.Buffers;
using Xunit.Abstractions;

namespace CryptoSolution;

/// <summary>
/// Demonstriert die Transformation von Daten aus einer Datenbank (simuliert durch IEnumerable/IQueryable)
/// und die anschließende Übergabe an die CryptoBridge.
/// </summary>
/// <param name="cache">Die zu verwendende Cache-Implementierung (Dependency Injection).</param>
/// <param name="logger">Optionaler Logger für den Test-Output.</param>
public class LinqValidationDemo(CryptoBridge bridge, ITestOutputHelper logger)
{
    /// <summary>
    /// Führt die LINQ-Abfrage und die Validierung aus.
    /// </summary>
    /// <param name="databaseDummy">Die Eingabedaten, die eine Datenbanktabelle repräsentieren.</param>
    public async Task RunLinqValidationDemoAsync(IEnumerable<CertificateRecord> databaseDummy)
    {
        byte[]? dataToValidate = databaseDummy
            .AsQueryable()
            .Where(c => c.IsActive && c.ValidUntil > DateTime.UtcNow)
            .OrderBy(c => c.ValidFrom)
            .Select(c => c.RawData)
            .FirstOrDefault();

        if(dataToValidate == null)
        {
            throw new InvalidOperationException("Keine gültigen Zertifikate in der Tabelle gefunden.");
        }

        logger?.WriteLine($"Verarbeite Zertifikat mit LINQ-Abfrage. Länge: {dataToValidate.Length} Bytes.");

        using (IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent(dataToValidate.Length))
        {
            Memory<byte> memory = memoryOwner.Memory.Slice(0, dataToValidate.Length);
            dataToValidate.CopyTo(memory);
            logger?.WriteLine("Daten in Memory gepuffert und bereit für die Übergabe an die CryptoBridge.");
            await bridge.RunIntegrationDemoAsync(memory);
        }
    }
}
