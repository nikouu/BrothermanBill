using System.Net;
using System.Web;
using BrothermanBill.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrothermanBill.Tests
{
    public class MemeServiceTests
    {
        [Fact]
        public async Task ReturnsTheSoundUrlOnASuccessfulSearch()
        {
            var service = CreateService("""
                {"count":1,"next":null,"previous":null,
                 "results":[{"name":"Bruh","slug":"bruh","sound":"https://x/bruh.mp3",
                             "color":"#fff","image":"","description":"","tags":""}]}
                """, out _);

            Assert.Equal("https://x/bruh.mp3", await service.GetMeme("bruh"));
        }

        [Fact]
        public async Task ReturnsNullWhenTheSearchHasNoResults()
        {
            var service = CreateService("""{"count":0,"next":null,"previous":null,"results":[]}""", out _);

            Assert.Null(await service.GetMeme("nothing matches this"));
        }

        [Fact]
        public async Task ReturnsNullWhenTheResultsKeyIsMissing()
        {
            // This shape used to reach the caller as a null array and throw there.
            var service = CreateService("""{"count":0,"next":null,"previous":null}""", out _);

            Assert.Null(await service.GetMeme("anything"));
        }

        [Fact]
        public async Task ReturnsNullOnAnErrorStatusCode()
        {
            var service = CreateService("Service Unavailable", out _, HttpStatusCode.ServiceUnavailable);

            Assert.Null(await service.GetMeme("anything"));
        }

        [Fact]
        public async Task ReturnsNullOnMalformedJson()
        {
            var service = CreateService("this is not json", out _);

            Assert.Null(await service.GetMeme("anything"));
        }

        [Fact]
        public async Task DoesNotThrowWhenTheApiIsUnreachable()
        {
            var service = new MemeService(
                new StubHttpClientFactory(new ThrowingHandler()),
                NullLogger<MemeService>.Instance);

            Assert.Null(await service.GetMeme("anything"));
        }

        [Fact]
        public async Task RandomMemeReturnsNullRatherThanIndexingAnEmptyResultSet()
        {
            // Random.Shared.Next(0) returns 0, so the old code indexed [0] of an empty array.
            var service = CreateService("""{"count":0,"next":null,"previous":null,"results":[]}""", out _);

            Assert.Null(await service.GetRandomMeme());
        }

        [Fact]
        public async Task EscapesTheSearchQuery()
        {
            var service = CreateService("""{"count":0,"next":null,"previous":null,"results":[]}""", out var handler);

            await service.GetMeme("rock & roll #1");

            Assert.NotNull(handler.LastRequestUri);

            // Unescaped, the "&" would start a second parameter and the "#" a fragment.
            // Round-tripping the value proves neither happened.
            var query = HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
            Assert.Equal("rock & roll #1", query.Get("name"));
            Assert.Equal("", handler.LastRequestUri.Fragment);
        }

        [Fact]
        public async Task RandomMemeRequestsAOneIndexedPage()
        {
            var service = CreateService("""{"count":0,"next":null,"previous":null,"results":[]}""", out var handler);

            await service.GetRandomMeme();

            var query = handler.LastRequestUri!.Query;
            if (query.Contains("page="))
            {
                var page = int.Parse(query.Split("page=")[1].Split('&')[0]);
                Assert.InRange(page, 2, 10);   // page 1 is sent without the parameter
            }
        }

        private static MemeService CreateService(string body, out RecordingHandler handler, HttpStatusCode status = HttpStatusCode.OK)
        {
            handler = new RecordingHandler(body, status);
            return new MemeService(new StubHttpClientFactory(handler), NullLogger<MemeService>.Instance);
        }

        private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }

        private sealed class RecordingHandler(string body, HttpStatusCode status) : HttpMessageHandler
        {
            public Uri? LastRequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
            }
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException("no route to host");
        }
    }
}
