# About
This console application pulls notifications from [SPIN Obvestila](https://spin.sos112.si/SPIN2/Javno/OD/) and pushes them to twitter feed of [@SPINObvestila](https://twitter.com/spinobvestila) account. It is run periodically.
# Build the project

Create Settings.nogit.cs file in directory Source/SpinTwitter.

Put in twitter credentials, like this:

```csharp
public static class Settings
    {
        public const string ConsumerKey = "***";
        public const string ConsumerSecret = "***";
        public const string AccessToken = "***";
        public const string AccessTokenSecret = "***";
        public const string ExceptionlessKey = "***";
    	public const string MastodonAccessToken = "***";
    }
```