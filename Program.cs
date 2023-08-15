using Microsoft.Extensions.Configuration;
using Mastonet;

HttpClient client = new HttpClient();

var config = new ConfigurationBuilder()
.AddJsonFile("appsettings.json", true)
.AddEnvironmentVariables()
.Build();
Uri? emonBaseUri = null;
if (string.IsNullOrEmpty(config["EMONCMS_BASE_URI"]) || !Uri.TryCreate(config["EMONCMS_BASE_URI"], UriKind.Absolute, out emonBaseUri))
{
    Console.Error.WriteLine("Missing or Invalid EmonCMS base URI");
    Environment.Exit(1);
}

if (string.IsNullOrEmpty(config["EMONCMS_READ_API_KEY"]))
{
    Console.Error.WriteLine("EmonCMS Read API Key is required");
    Environment.Exit(1);
}

var emonClient = new emoncmsmasto.EmonCMS(emonBaseUri, config["EMONCMS_READ_API_KEY"]!);
var now = DateTime.Now;
var solarFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_SOLAR_KWH_FEED_ID"]!), now.AddDays(-1), now);
var useFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_USE_KWH_FEED_ID"]!), now.AddDays(-1), now);
var importFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_IMPORT_KWH_FEED_ID"]!), now.AddDays(-1), now);
var exportFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_EXPORT_KWH_FEED_ID"]!), now.AddDays(-1), now);
var import = Math.Round(importFeed[1].reading - importFeed[0].reading, 2);
var export = Math.Round(exportFeed[1].reading - exportFeed[0].reading, 2);
var rate = double.Parse(config["RATE"]!);
var message = string.Format(@"
{0} stats for {1}
-------------------------------------
Generated {2} kWh from 🌞
🏡 used {3} kWh
Imported {4} kWh @ {6}/kWh = {7}
Exported {5} kWh @ {6}/kWh = {8}
{9}
",
config["BOT_NAME"],
now.AddDays(-1).ToString("yyyy/MM/dd"),
Math.Round(solarFeed[1].reading - solarFeed[0].reading, 2),
Math.Round(useFeed[1].reading - useFeed[0].reading, 2),
import,
export,
rate.ToString("C3"),
(import * rate).ToString("C"),
(export * rate).ToString("C"),
config["HASHTAGS"]
);
Console.Out.WriteLine($"Sending Message to Mastodon: {message}");
var mastoClient = new MastodonClient(config["MASTODON_URI"]!, config["MASTODON_API_KEY"]!, client);

var publish = true;
if (!string.IsNullOrEmpty(config["PUBLISH"]))
{
    if (!bool.TryParse(config["PUBLISH"], out publish))
    {
        publish = true;
    }
}
if (publish)
{
    await mastoClient.PublishStatus(message, Visibility.Public);
}