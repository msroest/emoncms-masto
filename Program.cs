using Microsoft.Extensions.Configuration;
using Mastonet;
using System.Globalization;
using ScottPlot;

HttpClient client = new HttpClient();

var config = new ConfigurationBuilder()
.AddJsonFile("appsettings.json", true)
.AddEnvironmentVariables()
.Build();
if (!string.IsNullOrEmpty(config["CULTURE"]))
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(config["CULTURE"]!);
}
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

if (string.IsNullOrEmpty(config["EMONCMS_SOLAR_KWH_FEED_ID"]) ||
   string.IsNullOrEmpty(config["EMONCMS_USE_KWH_FEED_ID"]) ||
   string.IsNullOrEmpty(config["EMONCMS_IMPORT_KWH_FEED_ID"]) ||
   string.IsNullOrEmpty(config["EMONCMS_EXPORT_KWH_FEED_ID"]) ||
   string.IsNullOrEmpty(config["EMONCMS_SOLAR_FEED_ID"]) ||
   string.IsNullOrEmpty(config["EMONCMS_USE_FEED_ID"]))
{
    Console.Error.WriteLine("Missing required feed id");
    Environment.Exit(1);
}

var emonClient = new emoncmsmasto.EmonCMS(emonBaseUri, config["EMONCMS_READ_API_KEY"]!);
var now = DateTime.Today;
var yesterday = now.AddDays(-1);
var solarFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_SOLAR_KWH_FEED_ID"]!), yesterday, now);
var useFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_USE_KWH_FEED_ID"]!), yesterday, now);
var importFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_IMPORT_KWH_FEED_ID"]!), yesterday, now);
var exportFeed = await emonClient.GetDailyFeedData(int.Parse(config["EMONCMS_EXPORT_KWH_FEED_ID"]!), yesterday, now);
var solarPowerFeed = await emonClient.GetFeedData(int.Parse(config["EMONCMS_SOLAR_FEED_ID"]!), yesterday, now.AddSeconds(-1));
var usePowerFeed = await emonClient.GetFeedData(int.Parse(config["EMONCMS_USE_FEED_ID"]!), yesterday, now.AddSeconds(-1));

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
var plt = new ScottPlot.Plot();
var solarFill = plt.Add.FillY(solarPowerFeed.Select(x => x.dateTime.ToOADate()).ToArray(), solarPowerFeed.Select(x => x.reading).ToArray(), solarPowerFeed.Select(x => 0.0).ToArray());
var useFill = plt.Add.FillY(usePowerFeed.Select(x => x.dateTime.ToOADate()).ToArray(), usePowerFeed.Select(x => x.reading).ToArray(), usePowerFeed.Select(x => 0.0).ToArray());

solarFill.Label = "Solar";
useFill.Label = "Use";

useFill.FillStyle.Color = Colors.Blue.WithAlpha(175);
solarFill.FillStyle.Color = Colors.Gold;
plt.AxisStyler.DateTimeTicks(Edge.Bottom);
plt.Style.Background(figure: new Color(51, 51, 51), data: new Color(51, 51, 51));
plt.Style.ColorAxes(Color.FromHex("#FFFFFF"));
plt.Style.ColorGrids(Color.FromHex("#FFFFFF"));
plt.YAxis.Label.Text = "Watts";

plt.Title($"Solar Generation for {yesterday.ToString("yyyy/MM/dd")}");
plt.AutoScale();
plt.Margins(0, 0.05);
plt.Legend.IsVisible = true;
var legend = plt.Legend;
legend.Location = Alignment.UpperLeft;
legend.Font.Size = 36;
legend.Font.Color = Colors.White;
legend.BackgroundFill.Color = new Color(51, 51, 51);


plt.SavePng("./output/generation.png", 1920, 1080);

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
    using (var fs = File.OpenRead("./output/generation.png"))
    {
        var media = await mastoClient.UploadMedia(fs, $"{yesterday.ToString("yyyy-MM-dd")}.png",
        $"Graph of Solar generation and home power usage of a home in Calgary, Alberta, Canada for {yesterday.ToString("yyyy-MM-dd")}");
        await mastoClient.PublishStatus(message, visibility: Visibility.Public, mediaIds: new[] { media.Id });
    }
}
