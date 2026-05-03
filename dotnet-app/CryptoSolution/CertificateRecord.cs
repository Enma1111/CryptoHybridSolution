namespace CryptoSolution;

/// <summary>
/// Repräsentiert die Tabellen-Entität, wie sie aus einer Datenbank geladen werden könnte.
/// </summary>
public class CertificateRecord
{
    public int Id { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsActive { get; set; }
}
