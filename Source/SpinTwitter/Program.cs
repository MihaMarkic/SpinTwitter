﻿using Exceptionless;
using Newtonsoft.Json;
using NLog;
using SpinTwitter.Models;
using SpinTwitter.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        private const string PersistanceFileName = "persistance.json";
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            logger.Info("*********************\nStarting v" + Assembly.GetExecutingAssembly().GetName().Version);
            ExceptionlessClient exceptionless = ExceptionlessClient.Default;
#if !DEBUG
            exceptionless.Configuration.ServerUrl = "https://except.rthand.com";
            exceptionless.Startup(Settings.ExceptionlessKey);

            exceptionless.CreateLog("Started sweep", Exceptionless.Logging.LogLevel.Info).Submit();
#endif

            int publishedTweets = 0;
            int failedTweets = 0;

            TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
            try
            {
                var creds = new TwitterCredentials(
                    Settings.ConsumerKey,
                    Settings.ConsumerSecret,
                    Settings.AccessToken,
                    Settings.AccessTokenSecret);
                Auth.SetCredentials(creds);

                var user = await UserAsync.GetAuthenticatedUser();
                logger.Info($"User is {user.Id}");
                var lastPublished = GetLastPublished();
                logger.Info($"Last published entered: {lastPublished.LastEntered} verified: {lastPublished.LastVerified}");

                using SpinRss rss = new SpinRss();

                try
                {
                    var rateLimits = RateLimit.GetCurrentCredentialsRateLimits();
                    if (rateLimits is null)
                    {
                        string errorMessage = "Couldn't retrieve rate limits, will exit since something went wrong";
                        logger.Error(errorMessage);
                        exceptionless.CreateException(new Exception(errorMessage)).Submit();
                    }
                    else
                    {
                        logger.Info("Rate limit access remaining {0}, reset on {1}",
                            rateLimits.StatusesHomeTimelineLimit.Remaining, rateLimits.StatusesHomeTimelineLimit.ResetDateTime);
                    }

                    var rssEnteredItems = await rss.GetFeedAsync(SpinRssType.Entered, CancellationToken.None);
                    var rssVerifiedItems = await rss.GetFeedAsync(SpinRssType.Verified, CancellationToken.None);

                    var newValues = rssEnteredItems.TakeNew(lastPublished.LastEntered).Reverse()
                        .Union(rssVerifiedItems.TakeNew(lastPublished.LastVerified).Reverse())
                        .ToImmutableArray();

                    logger.Info($"There are {newValues.Length} new tweets to publish");
                    foreach (var item in newValues)
                    {
                        bool success = ProcessRssItem(exceptionless, lastPublished, item);
                        if (success)
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
                        }
                        else
                        {
                            failedTweets++;
                        }
                    }
                }
                finally
                {
                    StoreLastPublished(lastPublished);
                }
            }
            catch (Exception ex)
            {
                ex.ToExceptionless().Submit();
                logger.Error(ex, "General failure");
            }
            exceptionless.CreateLog($"Done sweep with published {publishedTweets} and {failedTweets} failures", Exceptionless.Logging.LogLevel.Info).Submit();
            exceptionless.ProcessQueue();
        }

        static bool ProcessRssItem(ExceptionlessClient exceptionless, PersistenceRss lastPublished, RssItem item)
        {
            string tweetmsg = CreateTweet(item);

            logger.Info("Twitter message (length {1}) is '{0}'", tweetmsg, tweetmsg.Length);

            try
            {
                var tweet = Tweet.PublishTweet(tweetmsg);
                if (tweet == null)
                {
                    var exception = ExceptionHandler.GetLastException();

                    if (exception != null)
                    {
                        string errorMessage = $"Failed publishing tweet, got null as ITweet: StatusCode={exception.StatusCode} Description='{exception.TwitterDescription}' Details='{exception.TwitterExceptionInfos.FirstOrDefault()?.Message}'";
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
                    //logger.Info("Published tweet {0} success.", tweet.Id);
                    //lastPublished.Dates.Add(item.ReportDate);
                    StoreLastPublished(lastPublished);
                    logger.Info("Tweet publication state persisted with lastPublished {0}", lastPublished);
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

        public static string CreateTweet(RssItem value)
        {
            const int maxLen = 270;
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
            string fileName = GetFilePath(PersistanceFileName);
            if (File.Exists(fileName))
            {
                string content = File.ReadAllText(fileName);
                var persistance = JsonConvert.DeserializeObject<PersistenceRss>(content);
                return persistance;
            }
            else
                return new PersistenceRss();
        }

        static void StoreLastPublished(PersistenceRss value)
        {
            string fileName = GetFilePath(PersistanceFileName);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.WriteAllText(fileName, JsonConvert.SerializeObject(value));
        }

        static string GetFilePath(string name)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, name);
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
                return items.TakeWhile(i => i.Id != last.Value);
            }
        }
    }
}
