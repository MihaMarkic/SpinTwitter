using Exceptionless;
using Newtonsoft.Json;
using NLog;
using SpinTwitter.Models;
using SpinTwitter.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;

[assembly: InternalsVisibleTo("SpinTwitter.Test")]
namespace SpinTwitter
{
    public class Program
    {
        const string PersistanceFileName = "persistance.json";
        static readonly string PersistanceDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "state");
        static readonly string PersistancePath = Path.Combine(PersistanceDirectory, PersistanceFileName);
        static readonly Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Signals request to exit app.
        /// </summary>
        static readonly CancellationTokenSource cts = new CancellationTokenSource();
        static readonly ManualResetEvent ended = new ManualResetEvent(false);
        static async Task Main(string[] args)
        {
            logger.Info("*********************\nStarting v" + Assembly.GetExecutingAssembly().GetName().Version);
            ExceptionlessClient exceptionless = ExceptionlessClient.Default;
#if !DEBUG
            exceptionless.Configuration.ServerUrl = "https://except.rthand.com";
            exceptionless.Startup(Settings.ExceptionlessKey);

            exceptionless.CreateLog("Started sweep", Exceptionless.Logging.LogLevel.Info).Submit();
#endif

            TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            try
            {
                var creds = new TwitterCredentials(
                    Settings.ConsumerKey,
                    Settings.ConsumerSecret,
                    Settings.AccessToken,
                    Settings.AccessTokenSecret);
                Auth.SetCredentials(creds);

                var user = await UserAsync.GetAuthenticatedUser();
                logger.Info($"Twitter user is {user.Id}");
                PersistenceRss lastPublished = GetLastPublished();
                logger.Info($"Last published entered: {lastPublished.LastEntered} verified: {lastPublished.LastVerified}");
                var httpClient = new HttpClient();
                MastodonProvider mastodon = new MastodonProvider(httpClient, "https://botsin.space", Settings.MastodonAccessToken);
                SpinRss rss = new SpinRss(httpClient);
                {
                    while (!cts.IsCancellationRequested)
                    {
                        Task span = Task.Delay(TimeSpan.FromMinutes(5), cts.Token);
                        await LoopAsync(rss, mastodon, exceptionless, lastPublished, cts.Token);
                        await span;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info("Main loop was cancelled");
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                logger.Error(ex, "General failure");
            }
            finally
            {
                exceptionless.ProcessQueue();
                ended.Set();
            }
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
            }
        }

        static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            logger.Info("Exit request received");
            cts.Cancel();
            logger.Info("Waiting for end");
            ended.WaitOne();
            logger.Info("Ended confirmed");
        }

        static async Task LoopAsync(SpinRss rss, MastodonProvider mastodon, ExceptionlessClient exceptionless, PersistenceRss lastPublished, CancellationToken ct)
        {
            int publishedTweets = 0;
            int failedTweets = 0;

            try
            {
                var rateLimits = RateLimit.GetCurrentCredentialsRateLimits(useRateLimitCache: true);
                if (rateLimits is null)
                {
                    string errorMessage = "Couldn't retrieve rate limits, will exit since something went wrong";
                    logger.Warn(errorMessage);
                    //exceptionless.CreateException(new Exception(errorMessage)).Submit();
                }
                else
                {
                    logger.Info("Rate limit access remaining {0}, reset on {1}",
                        rateLimits.StatusesHomeTimelineLimit.Remaining, rateLimits.StatusesHomeTimelineLimit.ResetDateTime);
                }

                var rssEnteredItems = await rss.GetFeedAsync(SpinRssType.Entered, CancellationToken.None);
                var rssVerifiedItems = await rss.GetFeedAsync(SpinRssType.Verified, CancellationToken.None);

                var newEnteredItems = rssEnteredItems.TakeNew(lastPublished.LastEntered).Reverse().ToImmutableArray();
                var newVerifiedItems = rssVerifiedItems.TakeNew(lastPublished.LastVerified).Reverse().ToImmutableArray();
                logger.Info($"{newEnteredItems.Length} new entered:{string.Join(", ", newEnteredItems.Select(i => i.Id))}");
                logger.Info($"{newVerifiedItems.Length} new verified:{string.Join(", ", newVerifiedItems.Select(i => i.Id))}");
                var newValues = newEnteredItems.Union(newVerifiedItems).ToImmutableArray();

                logger.Info($"There are total {newValues.Length} new tweets to publish");
                foreach (var item in newValues)
                {
                    logger.Info($"Publishing {item.Type}:{item.Id}");
                    bool tweetSuccess = ProcessTwitterRssItem(exceptionless, item);
                    bool mastodonSuccess = await ProcessMastodonRssItem(mastodon, exceptionless, item, ct);
                    //if (tweetSuccess && mastodonSuccess)
                    {
                        publishedTweets++;
                        switch (item.Type)
                        {
                            case SpinRssType.Entered:
                                lastPublished.LastEntered = item.Id;
                                break;
                            case SpinRssType.Verified:
                                lastPublished.LastVerified = item.Id;
                                break;
                        }
                        StoreLastPublished(lastPublished);
                        logger.Info("Tweet publication state persisted with lastPublished {0}", lastPublished);
                    }
                    //else
                    //{
                    //    failedTweets++;
                    //}
                }
            }
            finally
            {
                exceptionless.CreateLog($"Done sweep with published {publishedTweets} and {failedTweets} failures", Exceptionless.Logging.LogLevel.Info).Submit();
            }
        }
        static async Task<bool> ProcessMastodonRssItem(MastodonProvider provider, ExceptionlessClient exceptionless, RssItem item, CancellationToken ct)
        { 
            // TODO retrieve max length from server when API becomes available
            string toot = CreateMessage(item, 500);
            try
            {
                string key = $"{item.Type}_{item.Id}";
                var response = await provider.TootAsync(toot, key, ct: ct);
                if (!response.IsSuccess)
                {
                    string errorMessage = $"Failed publishing toot {key}: {response.StatusCode}: {response.ReasonPhrase}";
                    logger.Error(errorMessage);
                    exceptionless.CreateException(new Exception(errorMessage)).Submit();
                }
                return response.IsSuccess;
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                logger.Error(ex, "Failed publishing tweet");
            }
            return false;
        }
        static bool ProcessTwitterRssItem(ExceptionlessClient exceptionless, RssItem item)
        {
            string tweetmsg = CreateMessage(item, 270);

            logger.Info($"Twitter message length is {tweetmsg.Length}", tweetmsg, tweetmsg.Length);

            try
            {
                var tweet = Tweet.PublishTweet(tweetmsg);
                if (tweet == null)
                {
                    var exception = ExceptionHandler.GetLastException();

                    if (exception != null)
                    {
                        string errorMessage = $"Failed publishing tweet {item.Id} of length {tweetmsg.Length}, got null as ITweet: StatusCode={exception.StatusCode} Description='{exception.TwitterDescription}' Details='{exception.TwitterExceptionInfos.FirstOrDefault()?.Message}'";
                        logger.Error(errorMessage);
                        exceptionless.CreateException(new Exception(errorMessage)).Submit();
                    }
                    else
                    {
                        logger.Error("Failed publishing tweet, got null as ITweet without exception");
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                logger.Error(ex, "Failed publishing tweet");
            }
            return false;
        }

        public static string CreateMessage(RssItem value, int maxLen)
        {
            string url = value.Link;
            string text = $"{value.Title}\n{value.Description}";
            string tweetmsg;
            int maxLenWithoutUrl = maxLen - url.Length;
            if (text.Length > maxLenWithoutUrl)
            {
                const string ellipsis = "...\n";
                int maxLenWithEllipsis = maxLenWithoutUrl - ellipsis.Length;
                tweetmsg = $"{text.Substring(0, maxLenWithEllipsis)}{ellipsis}{url}";
            }
            else
            {
                tweetmsg = $"{ text.Substring(0, Math.Min(text.Length, maxLen - 1))}\n{url}";
            }
            return tweetmsg;
        }

        static PersistenceRss GetLastPublished()
        {
            if (File.Exists(PersistancePath))
            {
                string content = File.ReadAllText(PersistancePath);
                var persistance = JsonConvert.DeserializeObject<PersistenceRss>(content);
                return persistance;
            }
            else
                return new PersistenceRss();
        }

        static void StoreLastPublished(PersistenceRss value)
        {
            if (!Directory.Exists(PersistanceDirectory))
            {
                Directory.CreateDirectory(PersistanceDirectory);
            }
            if (File.Exists(PersistancePath))
            {
                File.Delete(PersistancePath);
            }
            File.WriteAllText(PersistancePath, JsonConvert.SerializeObject(value));
        }

        //static string GetShortenUrl(HttpClient client, string originalUrl)
        //{
        //    try
        //    {
        //        var url = string.Format("http://api.bit.ly/v3/shorten?login={0}&apiKey={1}&format={2}&longUrl={3}",
        //                Settings.Username, Settings.APIKey, "xml", originalUrl);
        //        System.IO.Stream response = client.GetStreamAsync(url).Result;
        //        XDocument doc = XDocument.Load(response);
        //        XElement statusCodeNode = doc.Root.Element("status_code");
        //        if (statusCodeNode.Value == "200")
        //        {
        //            string shortenUrl = doc.Root.Element("data").Element("url").Value;
        //            return shortenUrl;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex, $"Failed shortening url {originalUrl}. Will use original.");
        //        return originalUrl;
        //    }
        //    return originalUrl;
        //}
    }

    public static class Extensions
    {
        public static IEnumerable<RssItem> TakeNew(this IEnumerable<RssItem> items, uint? last)
        {
            if (!last.HasValue)
            {
                return items;
            }
            else
            {
                return items.TakeWhile(i => i.Id > last.Value);
            }
        }
    }
}
