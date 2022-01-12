using BrothermanBill.Models;
using System.Text.Json;

namespace BrothermanBill.Services
{
    public class MemeService
    {
        private readonly string _memeUrl;

        public MemeService()
        {
            _memeUrl = "https://www.myinstants.com/api/v1/instants/";
        }

        public async Task<string> GetRandomMeme()
        {
            var random = new Random();
            var page = random.Next(10);
            var memeList = await SearchMemes("meme", page);

            var randomIndex = random.Next(memeList.Count());

            return memeList[randomIndex].Sound;
        }

        public async Task<string> GetMeme(string query)
        {
            var random = new Random();
            var memeList = await SearchMemes(query);

            if (memeList.Count() == 0)
            {
                return String.Empty;
            }
            else
            {
                //var randomIndex = random.Next(2);
                return memeList?[0]?.Sound;
            }
        }

        private async Task<MyInstantsQueryResult[]> SearchMemes(string query, int page = 1)
        {
            var urlParams = $"?name={query}";
            if (page > 1)
            {
                urlParams = urlParams + $"&page={page}";
            }
            using var client = new HttpClient();

            var response = await client.GetAsync(_memeUrl + urlParams);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                var queryResult = JsonSerializer.Deserialize<MyInstantsQueryRoot>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return queryResult.Results;
            }
            else
            {
                return Array.Empty<MyInstantsQueryResult>();
            }
        }
    }
}