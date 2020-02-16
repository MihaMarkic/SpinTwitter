using SpinTwitter.Models;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SpinTwitter.Services.Implementation
{
    public class SpinRss: IDisposable
    {
        const string enteredRssUrl = "https://spin3.sos112.si/api/javno/ODRSS/true";
        const string verifiedRssUrl = "https://spin3.sos112.si/api/javno/ODRSS/false";
        readonly  HttpClient client = new HttpClient();

        public async Task<ImmutableArray<RssItem>> GetFeedAsync(SpinRssType rssType, CancellationToken ct)
        {
            string rssUrl = rssType switch
            {
                SpinRssType.Entered => enteredRssUrl,
                SpinRssType.Verified => verifiedRssUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(rssType))
            };
            using (Stream content = await client.GetStreamAsync(rssUrl))
            {
                var doc = XDocument.Load(content);
                var result = doc.Root.Element("channel").Elements("item").Select(e => ConvertToItem(rssType, e)).ToImmutableArray();
                return result;
            }
        }
        RssItem ConvertToItem(SpinRssType type, XElement source)
        {
            string guid = source.Element("guid").Value;
            ReadOnlySpan<char> guidSpan = guid.AsSpan();
            string pubDate = source.Element("pubDate").Value;

            return new RssItem(
                type,
                id: ExtractId(guidSpan) ?? throw new Exception("Invalid Id"),
                title: source.Element("title").Value,
                link: source.Element("link").Value,
                description: source.Element("description").Value,
                guid: guid,
                pubDate: DateTimeOffset.TryParse(pubDate, out var date) ? date : throw new Exception($"Invalid date {pubDate}")
            );
        }

        internal uint? ExtractId(ReadOnlySpan<char> source)
        {
            int lastSlash = source.LastIndexOf('/');
            if (uint.TryParse(source.Slice(lastSlash+1), out uint result))
            {
                return result;
            }
            return null;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }

}
