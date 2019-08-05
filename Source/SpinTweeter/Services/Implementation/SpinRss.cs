namespace SpinTwitter.Services.Implementation
{
    public class SpinRss
    {
        //HttpClient client = new HttpClient();
        //var stream = client.GetStreamAsync("http://spin.sos112.si/SPIN2/Javno/OD/Rss.aspx").Result;
        //XmlReader reader = XmlReader.Create(stream);
        //SyndicationFeed feed = SyndicationFeed.Load(reader);
        //logger.Info("Feed loaded");

        //            var query = from i in feed.Items.Reverse()
        //                        let id = int.Parse(i.Id)
        //                        where id > lastPublished
        //                        select i;
        //            foreach (var item in query)
        //            {
        //                string entireSummary = item.Summary.Text;
        //int start = entireSummary.IndexOf(delimiter);
        //string summary = entireSummary.Substring(start + delimiter.Length);

        //logger.Info("Publishing item {0}, with summary length {1}", item.Id, summary.Length);

        //                string itemUrl = item.Links.OfType<SyndicationLink>().First().Uri.ToString();
        //string feedItemUrl = GetShortenUrl(client, itemUrl);
        ////Console.WriteLine(feedItemUrl);

        //int maxLen = 270;
        //string tweetmsg;
        //int maxLenWithoutUrl = maxLen - feedItemUrl.Length;
        //                if (summary.Length > maxLenWithoutUrl)
        //                {
        //                    int maxLenWithEllipsis = maxLenWithoutUrl - 3;
        //tweetmsg = string.Format("{0}...{1}", summary.Substring(0, maxLenWithEllipsis), feedItemUrl);
        //                }
        //                else
        //                {
        //                    tweetmsg = string.Format("{0} {1}", summary.Substring(0, Math.Min(summary.Length, maxLen - 1)), feedItemUrl);
        //                }
    }
}
