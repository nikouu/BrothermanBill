using BrothermanBill.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BrothermanBill.Services
{
    public class MemeService
    {
        private const string MemeUrl = "https://www.myinstants.com/api/v1/instants/";

        // Shared: a fresh JsonSerializerOptions per call also rebuilds the
        // serializer's metadata cache on every request.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MemeService> _logger;

        public MemeService(IHttpClientFactory httpClientFactory, ILogger<MemeService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>Returns a random meme sound URL, or <c>null</c> if none was found.</summary>
        public async Task<string?> GetRandomMeme()
        {
            // Pages are 1-indexed. Next(10) produced page 0, which the API treats as page 1.
            var page = Random.Shared.Next(1, 11);
            var memeList = await SearchMemes("meme", page);

            if (memeList.Length == 0)
            {
                return null;
            }

            return memeList[Random.Shared.Next(memeList.Length)].Sound;
        }

        /// <summary>Returns the first matching meme sound URL, or <c>null</c> if none was found.</summary>
        public async Task<string?> GetMeme(string query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var memeList = await SearchMemes(query);

            if (memeList.Length == 0)
            {
                return null;
            }

            return memeList[0].Sound;
        }

        private async Task<MyInstantsQueryResult[]> SearchMemes(string query, int page = 1)
        {
            var url = $"{MemeUrl}?name={Uri.EscapeDataString(query)}";
            if (page > 1)
            {
                url += $"&page={page}";
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MyInstants returned {Status} for {Query}.", response.StatusCode, query);
                    return Array.Empty<MyInstantsQueryResult>();
                }

                var data = await response.Content.ReadAsStringAsync();
                var queryResult = JsonSerializer.Deserialize<MyInstantsQueryRoot>(data, SerializerOptions);

                // Some responses omit "results" entirely, which deserializes to null.
                return queryResult.Results ?? Array.Empty<MyInstantsQueryResult>();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError(exception, "Failed to query MyInstants for {Query}.", query);
                return Array.Empty<MyInstantsQueryResult>();
            }
        }
    }
}
