using Exceptionless;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SpinTwitter.Test")]
namespace SpinTwitter
{
    public static class Program
    {
        const string PersistenceFileName = "persistance.json";
        static readonly string PersistenceDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "state");
        static readonly string PersistencePath = Path.Combine(PersistenceDirectory, PersistenceFileName);
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Signals request to exit app.
        /// </summary>
        static readonly CancellationTokenSource Cts = new CancellationTokenSource();
        static readonly ManualResetEvent Ended = new ManualResetEvent(false);
        static async Task Main(string[] args)
        {
            Logger.Info("*********************\nStarting v" + Assembly.GetExecutingAssembly().GetName().Version);
            ExceptionlessClient exceptionless = ExceptionlessClient.Default;
#if !DEBUG
            exceptionless.Configuration.ServerUrl = "https://except.rthand.com";
            exceptionless.Startup(Settings.ExceptionlessKey);

            exceptionless.CreateLog("Started sweep", Exceptionless.Logging.LogLevel.Info).Submit();
#endif

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            try
            {
                PersistenceRss lastPublished = GetLastPublished();
                Logger.Info($"Last published entered: {lastPublished.LastEntered} verified: {lastPublished.LastVerified}");
                var httpClient = new HttpClient();
                MastodonProvider mastodon = new MastodonProvider(httpClient, "https://mastodon.world", Settings.MastodonAccessToken);
                SpinRss rss = new SpinRss(httpClient);
                {
                    while (!Cts.IsCancellationRequested)
                    {
                        Task span = Task.Delay(TimeSpan.FromMinutes(5), Cts.Token);
                        await LoopAsync(rss, mastodon, exceptionless, lastPublished, Cts.Token);
                        await span;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Main loop was cancelled");
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                Logger.Error(ex, "General failure");
            }
            finally
            {
                await exceptionless.ProcessQueueAsync();
                Ended.Set();
            }
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
            }
        }

        static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            Logger.Info("Exit request received");
            Cts.Cancel();
            Logger.Info("Waiting for end");
            Ended.WaitOne();
            Logger.Info("Ended confirmed");
        }

        static async Task LoopAsync(SpinRss rss, MastodonProvider mastodon, ExceptionlessClient exceptionless, 
            PersistenceRss lastPublished, CancellationToken ct)
        {
            int publishedTweets = 0;
            int failedTweets = 0;

            try
            {
                var rssEnteredItems = await rss.GetFeedAsync(SpinRssType.Entered, CancellationToken.None);
                var rssVerifiedItems = await rss.GetFeedAsync(SpinRssType.Verified, CancellationToken.None);

                var newEnteredItems = rssEnteredItems.TakeNew(lastPublished.LastEntered).Reverse().ToImmutableArray();
                var newVerifiedItems = rssVerifiedItems.TakeNew(lastPublished.LastVerified).Reverse().ToImmutableArray();
                var newValues = newEnteredItems.AddRange(newVerifiedItems).ToImmutableArray();

                if (newValues.Length > 0)
                {
                    if (newEnteredItems.Length > 0)
                    {
                        Logger.Info($"{newEnteredItems.Length} new entered:{string.Join(", ", newEnteredItems.Select(i => i.Id))}");
                    }
                    if (newVerifiedItems.Length > 0)
                    {
                        Logger.Info($"{newVerifiedItems.Length} new verified:{string.Join(", ", newVerifiedItems.Select(i => i.Id))}");
                    }
                    Logger.Info($"There are total {newValues.Length} new tweets to publish");
                    foreach (var item in newValues)
                    {
                        Logger.Info($"Publishing {item.Type}:{item.Id}");
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
                            Logger.Info("Tweet publication state persisted with lastPublished {0}", lastPublished);
                        }
                        //else
                        //{
                        //    failedTweets++;
                        //}
                    }
                }
                else
                {
                    Logger.Info("No new entries");
                }
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().SetMessage("Failed processing loop").Submit();
                Logger.Error(ex, "Failed processing loop");
            }
            exceptionless.CreateLog($"Done sweep with published {publishedTweets} and {failedTweets} failures", Exceptionless.Logging.LogLevel.Info).Submit();
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
                    Logger.Error(errorMessage);
                    exceptionless.CreateException(new Exception(errorMessage)).Submit();
                }
                return response.IsSuccess;
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                Logger.Error(ex, "Failed publishing tweet");
            }
            return false;
        }
        public static string CreateMessage(RssItem value, int maxLen)
        {
            string url = value.Link;
            string text = $"{value.Title}\n{value.Description}";
            string message;
            int maxLenWithoutUrl = maxLen - url.Length;
            if (text.Length > maxLenWithoutUrl)
            {
                const string ellipsis = "...\n";
                int maxLenWithEllipsis = maxLenWithoutUrl - ellipsis.Length;
                message = $"{text.Substring(0, maxLenWithEllipsis)}{ellipsis}{url}";
            }
            else
            {
                message = $"{ text.Substring(0, Math.Min(text.Length, maxLen - 1))}\n{url}";
            }
            return message;
        }

        static PersistenceRss GetLastPublished()
        {
            if (File.Exists(PersistencePath))
            {
                string content = File.ReadAllText(PersistencePath);
                var persistence = JsonSerializer.Deserialize<PersistenceRss>(content);
                return persistence ?? new PersistenceRss();
            }
            else
                return new PersistenceRss();
        }

        static void StoreLastPublished(PersistenceRss value)
        {
            if (!Directory.Exists(PersistenceDirectory))
            {
                Directory.CreateDirectory(PersistenceDirectory);
            }
            if (File.Exists(PersistencePath))
            {
                File.Delete(PersistencePath);
            }
            File.WriteAllText(PersistencePath, JsonSerializer.Serialize(value));
        }
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
