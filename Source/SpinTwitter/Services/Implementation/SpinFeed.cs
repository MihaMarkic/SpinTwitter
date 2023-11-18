using SpinTwitter.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SpinTwitter.Services.Implementation
{
    public sealed class SpinFeed: IDisposable
    {
        readonly HttpClient client;
        public SpinFeed(HttpClient client)
        {
            this.client = client;
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public async Task<Root?> GetFeedAsync(CancellationToken ct)
        {
            var text = await client.GetStringAsync("https://spin3.sos112.si/api/javno/lokacija");
            var data = ConvertFromString(text);
            return data;
        }

        public static Root? ConvertFromString(string text)
        {
            return JsonSerializer.Deserialize<Root>(text); ;
        }
    }
}
