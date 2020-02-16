using System;
using System.Diagnostics;

namespace SpinTwitter.Models
{
    public enum SpinRssType
    {
        Entered,
        Verified
    }

    [DebuggerDisplay("{Title,nq}")]
    public readonly struct RssItem
    {
        public SpinRssType Type { get; }
        public uint Id { get; }
        public string Title { get; }
        public string Link { get; }
        public string Description { get; }
        public string Guid { get; }
        public DateTimeOffset PubDate { get; }

        public RssItem(SpinRssType type, uint id, string title, string link, string description, string guid, DateTimeOffset pubDate)
        {
            Type = type;
            Id = id;
            Title = title;
            Link = link;
            Description = description;
            Guid = guid;
            PubDate = pubDate;
        }
    }
}
