using Exceptionless;
using Newtonsoft.Json;
using NLog;
using SpinTwitter.Models;
using SpinTwitter.Properties;
using SpinTwitter.Services.Implementation;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Tweetinvi;
using Tweetinvi.Models;

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
            exceptionless.Configuration.ServerUrl = "https://except.rthand.com";
            exceptionless.Startup(Settings.Default.ExceptionlessKey);

            exceptionless.CreateLog("Started sweep", Exceptionless.Logging.LogLevel.Info).Submit();

            int publishedTweets = 0;
            int failedTweets = 0;

            TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
            try
            {
                var creds = new TwitterCredentials(
                    Settings.Default.token_ConsumerKey,
                    Settings.Default.token_ConsumerSecret,
                    Settings.Default.token_AccessToken,
                    Settings.Default.token_AccessTokenSecret);
                Auth.SetCredentials(creds);

                var lastPublished = GetLastPublished();
                logger.Info("Last published {0}", lastPublished);

                try
                {
                    var rateLimits = RateLimit.GetCurrentCredentialsRateLimits();
                    logger.Info("Rate limit access remaining {0}, reset on {1}",
                        rateLimits.StatusesHomeTimelineLimit.Remaining, rateLimits.StatusesHomeTimelineLimit.ResetDateTime);

                    Root root;
                    using (var feed = new SpinFeed())
                    {
                        root = await feed.GetFeedAsync(CancellationToken.None);
                    }

                    if (root.Value?.Length == 0)
                    {
                        logger.Warn("Didn't retrieve any feed items");
                        return;
                    }

                    PurgeLastPublished(lastPublished, root.Value);

                    var newValues = (from v in root.Value
                                    where !lastPublished.Dates.Contains(v.ReportDate)
                                    select v).Reverse().ToArray();

                    foreach (var item in newValues)
                    {
                        string tweetmsg = CreateTweet(item);

                        logger.Info("Twitter message (length {1}) is '{0}'", tweetmsg, tweetmsg.Length);

                        try
                        {
                            var tweet = Tweet.PublishTweet(tweetmsg);
                            if (tweet == null)
                            {
                                failedTweets++;
                                var exception = ExceptionHandler.GetLastException();

                                if (exception != null)
                                {
                                    string errorMessage = $"Failed publishing tweet, got null as ITweet: StatusCode={exception.StatusCode} Description='{exception.TwitterDescription}' Details='{exception.TwitterExceptionInfos.First().Message}'";
                                    logger.Error(errorMessage);
                                    exceptionless.CreateException(new Exception(errorMessage)).Submit(); ;
                                }
                                else
                                {
                                    logger.Error("Failed publishing tweet, got null as ITweet without exception");
                                }
                            }
                            else
                            {
                                publishedTweets++;
                                //logger.Info("Published tweet {0} success.", tweet.Id);
                                lastPublished.Dates.Add(item.ReportDate);
                                StoreLastPublished(lastPublished);
                                logger.Info("Tweet publication state persisted with lastPublished {0}", lastPublished);
                            }
                        }
                        catch (Exception ex)
                        {
                            ex.ToExceptionless().Submit();
                            logger.Error(ex, "Failed publishing tweet");
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

        public static void PurgeLastPublished(Persistence persistence, Value[] values)
        {
            var dates = values.Select(d => d.ReportDate).ToArray();
            for (int i = persistence.Dates.Count-1; i >= 0; i--)
            {
                if (!dates.Contains(persistence.Dates[i]))
                {
                    persistence.Dates.RemoveAt(i);
                }
            }
        }

        public static string CreateTweet(Value value)
        {
            const int maxLen = 270;
            const string url = "https://spin3.sos112.si/javno/pregled";
            string text = $"{value.Title}\n{value.ReportDate:dd.MM HH:mm}\n{value.Text}";
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

        static Persistence GetLastPublished()
        {
            string fileName = GetFilePath(PersistanceFileName);
            if (File.Exists(fileName))
            {
                string content = File.ReadAllText(fileName);
                var persistance = JsonConvert.DeserializeObject<Persistence>(content);
                return persistance;
            }
            else
                return new Persistence();
        }

        static void StoreLastPublished(Persistence value)
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
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), name);
        }

        static string GetShortenUrl(HttpClient client, string originalUrl)
        {
            try
            {
                var url = string.Format("http://api.bit.ly/v3/shorten?login={0}&apiKey={1}&format={2}&longUrl={3}",
                        Settings.Default.bitly_Username, Settings.Default.bitly_APIKey, "xml", originalUrl);
                System.IO.Stream response = client.GetStreamAsync(url).Result;
                XDocument doc = XDocument.Load(response);
                XElement statusCodeNode = doc.Root.Element("status_code");
                if (statusCodeNode.Value == "200")
                {
                    string shortenUrl = doc.Root.Element("data").Element("url").Value;
                    return shortenUrl;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed shortening url {originalUrl}. Will use original.");
                return originalUrl;
            }
            return originalUrl;
        }
    }
}
