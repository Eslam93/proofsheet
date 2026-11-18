using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proofsheet.Infrastructure.Clients;
using Proofsheet.Infrastructure.Options;

namespace Proofsheet.Tests.Unit.Providers;

public sealed class SecEdgarClientTests
{
    [Fact]
    public async Task Disabled_Client_Sends_No_Request_Even_With_A_User_Agent()
    {
        var handler = new RecordingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new SecEdgarClient(
            new HttpClient(handler),
            Options.Create(new SecEdgarOptions
            {
                Enabled = false,
                DataBaseUrl = "https://data.sec.gov/",
                MappingUrl = "https://www.sec.gov/files/company_tickers_exchange.json",
                UserAgent = "Proofsheet-Test/1.0 test@example.com"
            }),
            cache,
            NullLogger<SecEdgarClient>.Instance);

        Assert.Null(await client.GetTickerProfileAsync("AAPL", CancellationToken.None));
        Assert.Empty(await client.SearchTickerProfilesAsync("ap", 10, CancellationToken.None));
        Assert.Null(await client.GetSubmissionsAsync("AAPL", CancellationToken.None));
        Assert.Null(await client.GetCompanyFactsAsync("AAPL", CancellationToken.None));
        Assert.Equal(0, handler.Requests);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            throw new InvalidOperationException($"No request expected, got {request.RequestUri}.");
        }
    }
}
