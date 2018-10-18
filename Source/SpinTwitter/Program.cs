using Exceptionless;
using NLog;
using SpinTwitter.Properties;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using Tweetinvi;
using Tweetinvi.Models;

namespace SpinTwitter
{
    class Program
    {
        private const string CounterFileName = "counter.txt";
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            logger.Info("*********************\nStarting v" + Assembly.GetExecutingAssembly().GetName().Version);
            ExceptionlessClient exceptionless = ExceptionlessClient.Default;
            exceptionless.Configuration.ServerUrl = "https://except.rthand.com";
            exceptionless.Startup(Settings.Default.ExceptionlessKey);

            exceptionless.CreateLog("Started sweep", Exceptionless.Logging.LogLevel.Info).Submit();

            const string delimiter = "<br>";
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

                int lastPublished = GetLastPublished();
                logger.Info("Last published {0}", lastPublished);

                try
                {
                    var rateLimits = RateLimit.GetCurrentCredentialsRateLimits();
                    logger.Info("Rate limit access remaining {0}, reset on {1}",
                        rateLimits.StatusesHomeTimelineLimit.Remaining, rateLimits.StatusesHomeTimelineLimit.ResetDateTime);

                    HttpClient client = new HttpClient();
                    var stream = client.GetStreamAsync("http://spin.sos112.si/SPIN2/Javno/OD/Rss.aspx").Result;
                    XmlReader reader = XmlReader.Create(stream);
                    SyndicationFeed feed = SyndicationFeed.Load(reader);
                    logger.Info("Feed loaded");

                    var query = from i in feed.Items.Reverse()
                                let id = int.Parse(i.Id)
                                where id > lastPublished
                                select i;
                    foreach (var item in query)
                    {
                        string entireSummary = item.Summary.Text;
                        int start = entireSummary.IndexOf(delimiter);
                        string summary = entireSummary.Substring(start + delimiter.Length);

                        logger.Info("Publishing item {0}, with summary length {1}", item.Id, summary.Length);

                        string itemUrl = item.Links.OfType<SyndicationLink>().First().Uri.ToString();
                        string feedItemUrl = GetShortenUrl(client, itemUrl);
                        //Console.WriteLine(feedItemUrl);

                        int maxLen = 270;
                        string tweetmsg;
                        int maxLenWithoutUrl = maxLen - feedItemUrl.Length;
                        if (summary.Length > maxLenWithoutUrl)
                        {
                            int maxLenWithEllipsis = maxLenWithoutUrl - 3;
                            tweetmsg = string.Format("{0}...{1}", summary.Substring(0, maxLenWithEllipsis), feedItemUrl);
                        }
                        else
                        {
                            tweetmsg = string.Format("{0} {1}", summary.Substring(0, Math.Min(summary.Length, maxLen - 1)), feedItemUrl);
                        }
                        //Console.WriteLine("----");
                        //Console.WriteLine(tweetmsg);
                        logger.Info("Twitter message (length {1}) is '{0}'", tweetmsg, tweetmsg.Length);

                        try
                        {
                            ITweet tweet = Tweet.PublishTweet(tweetmsg);
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
                                logger.Info("Published tweet {0} success.", tweet.Id);
                                lastPublished = Math.Max(lastPublished, int.Parse(item.Id));
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


        private static int GetLastPublished()
        {
            string fileName = GetFilePath(CounterFileName);
            if (File.Exists(fileName))
            {
                string content = File.ReadAllText(fileName);
                int result = int.Parse(content);
                return result;
            }
            else
                return 0;
        }

        private static void StoreLastPublished(int value)
        {
            string fileName = GetFilePath(CounterFileName);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.WriteAllText(fileName, value.ToString());
        }

        private static string GetFilePath(string name)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), name);
        }

        private static string GetShortenUrl(HttpClient client, string originalUrl)
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
