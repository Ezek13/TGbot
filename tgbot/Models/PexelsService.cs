using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace tgbot.Models
{
    public static class PexelsService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string _apiKey = "nIOCbfYTG4yujs6MRxXnyRzcqMGqf0M9dLs2L2ETMgANfi73cCaV4Xuh"; // ВСТАВ КЛЮЧ СЮДИ

        public static async Task<string> GetExerciseVideoAsync(string query)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", _apiKey);

                // Запит №1: Пошук відео (Вимога лаби)
                string url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page=5";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PexelsResponse>(json);

                // ОБРОБКА ДАНИХ (Вимога лаби):
                // 1. Фільтрація (вибираємо відео коротше 30 секунд, щоб не перевантажувати Telegram)
                // 2. Сортування (від найкоротшого відео до найдовшого)
                var bestVideo = result?.Videos
                    .Where(v => v.Duration < 35)
                    .OrderBy(v => v.Duration)
                    .FirstOrDefault();

                // Вибираємо посилання на HD якість (якщо є) або перше ліпше
                return bestVideo?.VideoFiles
                    .OrderByDescending(f => f.Quality == "hd")
                    .FirstOrDefault()?.Link;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pexels Error]: {ex.Message}");
                return null;
            }
        }
    }
}