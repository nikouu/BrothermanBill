using BrothermanBill.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Victoria;

namespace BrothermanBill.Services
{
    public class MemeService
    {
        private readonly LavaNode _lavaNode;
        private readonly string _memeUrl;

        public MemeService(LavaNode lavaNode)
        {
            _lavaNode = lavaNode;
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
                var queryResult = JsonSerializer.Deserialize<MyInstantsQueryRoot>(data);
                return queryResult.Results;
            }
            else
            {
                return Array.Empty<MyInstantsQueryResult>();
            }
        }
    }
}