using System.Net;
using CoupDeSonde.Api.Security;
using Microsoft.AspNetCore.Http;

namespace CoupDeSonde.Tests;

public class ConnexionRateLimitingTests
{
    [Fact]
    public void CleDePartition_AdresseIpConnue_RetourneAdresseIp()
    {
        var contexte = new DefaultHttpContext();
        contexte.Connection.RemoteIpAddress = IPAddress.Loopback;

        var cle = ConnexionRateLimiting.CleDePartition(contexte);

        Assert.Equal(IPAddress.Loopback.ToString(), cle);
    }

    [Fact]
    public void CleDePartition_AdresseIpInconnue_RetourneValeurParDefaut()
    {
        var contexte = new DefaultHttpContext();
        contexte.Connection.RemoteIpAddress = null;

        var cle = ConnexionRateLimiting.CleDePartition(contexte);

        Assert.Equal("inconnu", cle);
    }
}
