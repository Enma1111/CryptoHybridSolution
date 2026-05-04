using System.Security.Cryptography;
using Xunit.Abstractions;

namespace CryptoSolution.Tests;

public class CryptoIntegrationTests(ITestOutputHelper logger)
{
    private readonly ITestOutputHelper _output = logger;

    [InlineData(new byte[] { 0x30, 0x11, 0x31, 0x0f, 0x30, 0x0d, 0x06, 0x03, 0x55, 0x04, 0x03, 0x13, 0x06, 0x54, 0x65, 0x73, 0x74, 0x43, 0x4e }, true)]
    [InlineData(new byte[] { 0xFF, 0x00, 0x01 }, false)]
    [InlineData(new byte[] { 0x30, 0x0A }, false)]
    [Theory]
    public void Test_Asn1_Validation_Scenarios(byte[] data, bool expectedResult)
    {
        // Act
        bool result = false;
        result = CryptoBridge.ValidateX509Structure(data);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task Test_Integration_With_Real_Certificate()
    {
        // Arrange
        byte[] certBytes = await File.ReadAllBytesAsync("testcert.cer");
        var fakeCache = new FakeCache();
        var bridge = new CryptoBridge(fakeCache, _output);
        string expectedHash = Convert.ToBase64String(SHA256.HashData(certBytes));

        // Act
        await bridge.RunIntegrationDemoAsync(certBytes);
        var cached = await fakeCache.GetAsync<bool>(expectedHash);

        // Assert
        Assert.True(cached);
    }
}