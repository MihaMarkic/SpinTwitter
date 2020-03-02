using SpinTwitter.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpinTwitter.Services.Implementation
{
    /// <summary>
    /// Contains functionality related to Mastodon REST API
    /// </summary>
    public sealed class MastodonProvider
    {
        readonly HttpClient httpClient;
        readonly string hostName;
        readonly string accessToken;
        /// <summary>
        /// Creates an object for sending toots to Mastodon using access-token.
        ///
        /// Get your authorization token by creating an app at Mastodon.
        /// </summary>
        public MastodonProvider(HttpClient httpClient, string hostName, string accessToken)
        {
            this.httpClient = httpClient;
            this.hostName = hostName?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(hostName));
            this.accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken)); ;
        }

        /// <summary>
        /// Sends a toot with the supplied text and returns the response from the Mastodon REST API along with status code and reason phrase.
        /// </summary>
        public Task<TootResponse> TootAsync(string text, string? idempotencyKey = null, TootVisibility visibility = TootVisibility.Public, 
            CancellationToken ct = default)
        {
            var data = new Dictionary<string, string> {
                { "status", text.Replace("\n", "\\n") },
                { "visibility", visibility.ToString().ToLower(CultureInfo.InvariantCulture) }
            };

            return SendRequestAsync($"{hostName}/api/v1/statuses", data, idempotencyKey, ct);
        }

        static string DictionaryToJson(Dictionary<string, string> data)
        {
            var entries = data.Select(d => $"\"{d.Key}\": \"{d.Value}\"");
            return $"{{{string.Join(",", entries)}}}";
        }
        async Task<TootResponse> SendRequestAsync(string url, Dictionary<string, string> data, string? idempotencyKey, CancellationToken ct)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }
            request.Content =  new StringContent(DictionaryToJson(data), Encoding.UTF8, "application/json");
            var response = await httpClient.SendAsync(request, ct);
            string body = await response.Content.ReadAsStringAsync();
            return new TootResponse(response.IsSuccessStatusCode, response.StatusCode, response.ReasonPhrase, body);
        }
    }
}
