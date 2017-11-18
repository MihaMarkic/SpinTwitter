# About
This console application pulls notifications from [SPIN Obvestila](https://spin.sos112.si/SPIN2/Javno/OD/) and pushes them to twitter feed of [@SPINObvestila](https://twitter.com/spinobvestila) account. It is run periodically.
# Build the project

Create Settings.settings file in directory Source/SpinTwitter/Properties.

Put in twitter credentials, like this:

```
<?xml version='1.0' encoding='utf-8'?>
<SettingsFile xmlns="http://schemas.microsoft.com/VisualStudio/2004/01/settings" CurrentProfile="(Default)">
  <Profiles>
    <Profile Name="(Default)" />
  </Profiles>
  <Settings>
    <Setting Name="token_ConsumerKey" Type="System.String" Scope="Application">
      <Value Profile="(Default)">CONSUMER_KEY</Value>
    </Setting>
    <Setting Name="token_ConsumerSecret" Type="System.String" Scope="Application">
      <Value Profile="(Default)">CONSUMER_SECRET</Value>
    </Setting>
    <Setting Name="token_AccessToken" Type="System.String" Scope="Application">
      <Value Profile="(Default)">ACCESS_TOKEN</Value>
    </Setting>
    <Setting Name="token_AccessTokenSecret" Type="System.String" Scope="Application">
      <Value Profile="(Default)">ACCESS_TOKEN_SECRET</Value>
    </Setting>
    <Setting Name="bitly_Username" Type="System.String" Scope="Application">
      <Value Profile="(Default)">BITLY_USERNAME</Value>
    </Setting>
    <Setting Name="bitly_APIKey" Type="System.String" Scope="Application">
      <Value Profile="(Default)">BITLY_APIKEY</Value>
    </Setting>
  </Settings>
</SettingsFile>
```