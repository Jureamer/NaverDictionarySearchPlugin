using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Wox.Plugin;

namespace MyNaverDictionaryPlugin
{
    public class Main : IPlugin
    {
        private PluginInitContext _context;

        public string Name => "Naver Dictionary";
        public string Description => "Search Naver dictionary";

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrWhiteSpace(query.Search))
                return new List<Result>();

            // 비동기 작업을 동기적으로 실행
            var results = GetDictionaryDataAsync(query.Search).GetAwaiter().GetResult();

            return results.Select(item => new Result
            {
                Title = item.Txt,
                SubTitle = item.Rtxt,
                IcoPath = "images/demo_dark.png", // 아이콘 경로 설정
                Action = _ => 
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = $"https://en.dict.naver.com/#/search?query={Uri.EscapeDataString(item.Txt)}",
                        UseShellExecute = true
                    });
                    return true;
                }
            }).ToList();
        }

        private async Task<List<DictionaryItem>> GetDictionaryDataAsync(string word)
        {
            string url = "https://ac-dict.naver.com/enko/ac";
            using (var httpClient = new HttpClient())
            {
                var queryParams = new Dictionary<string, string>
                {
                    { "q_enc", "utf-8" },
                    { "st", "11001" },
                    { "r_format", "json" },
                    { "r_enc", "utf-8" },
                    { "r_lt", "10001" },
                    { "r_unicode", "0" },
                    { "r_escape", "1" },
                    { "q", word }
                };

                var requestUri = $"{url}?{string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}";

                var response = await httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(jsonString);

                var items = new List<DictionaryItem>();

                if (jsonDoc.RootElement.TryGetProperty("items", out JsonElement itemsElement) 
                    && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var itemsArr in itemsElement.EnumerateArray())
                    {
                        if (itemsArr.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var itemArr in itemsArr.EnumerateArray())
                        {
                            if (itemArr.ValueKind != JsonValueKind.Array)
                                continue;

                            var subItems = itemArr.EnumerateArray().ToArray();

                            if (subItems.Length > 2 
                                && subItems[0].ValueKind == JsonValueKind.Array
                                && subItems[2].ValueKind == JsonValueKind.Array)
                            {
                                var txtArray = subItems[0].EnumerateArray().ToArray();
                                var rtxtArray = subItems[2].EnumerateArray().ToArray();

                                if (txtArray.Length > 0 && rtxtArray.Length > 0)
                                {
                                    string txt = txtArray[0].GetString() ?? "";
                                    string rtxt = rtxtArray[0].GetString() ?? "";

                                    items.Add(new DictionaryItem { Txt = txt, Rtxt = rtxt });
                                }
                            }
                        }
                    }
                }

                return items;
            }
        }

        private class DictionaryItem
        {
            public string Txt { get; set; }
            public string Rtxt { get; set; }
        }
    }
}
