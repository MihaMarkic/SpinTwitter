# About
This console application pulls notifications from [SPIN Obvestila](https://spin.sos112.si/SPIN2/Javno/OD/) and pushes them to ~~twitter feed of [@SPINObvestila](https://twitter.com/spinobvestila)~~ Mastodon feed of [SPINObvestila](https://botsin.space/@SpinObvestila) account. It is run periodically.
# Build the project

Create `Settings.nogit.cs` file in directory `Source/SpinTwitter`.

~~Put in twitter credentials, like this:~~

NOTE: Twitter support is removed with version 2.1.0.

Put in credentials, like this

```csharp
public static class Settings
    {
        public const string ExceptionlessKey = "***";
    	public const string MastodonAccessToken = "***";
    }
```