using Xunit.Abstractions;

namespace CryptoSolution.Tests;

public class LinqIntegrationTests(ITestOutputHelper logger)
{
    private readonly ITestOutputHelper _output = logger;

    [Fact]
    public async Task Test_Linq_To_Bridge_Conversion()
    {
        // Arrange
        FakeCache cache = new FakeCache();
        CryptoBridge bridge = new CryptoBridge(cache, _output);
        LinqValidationDemo demo = new LinqValidationDemo(bridge, _output);
        byte[] certBytes = await File.ReadAllBytesAsync("testcert.cer");

        var databaseDummy = new List<CertificateRecord>
        {
            new() {
                Id = 1,
                SubjectName = "Test-Cert-1",
                Issuer = "Root CA",
                RawData = certBytes,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidUntil = DateTime.UtcNow.AddDays(30),
                IsActive = true
            }
        };

        // Act & Assert
        await demo.RunLinqValidationDemoAsync(databaseDummy);
    }
}
