using Newtonsoft.Json;
using SpinTwitter.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SpinTwitter.Services.Implementation
{
    public class SpinFeed: IDisposable
    {
        readonly HttpClient client = new HttpClient();

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<Root> GetFeedAsync(CancellationToken ct)
        {
            var text = await client.GetStringAsync("https://spin3.sos112.si/api/javno/lokacija");
            var data = ConvertFromString(text);
            return data;
        }

        public static Root ConvertFromString(string text)
        {
            return JsonConvert.DeserializeObject<Root>(text); ;
        }
    }
}
