using System.Net.Http.Json;
using System.Text.Json.Nodes;
namespace emoncmsmasto
{
    public struct FeedReading
    {
        public DateTime dateTime;
        public double reading;
        public override string ToString() {
            return string.Format("{0}-{1}", dateTime.ToString(), reading.ToString());
        }
    }
    public class EmonCMS
    {
        public Uri BaseUri
        {
            get; private set;
        }
        public string ApiKey
        {
            get; private set;
        }
        public EmonCMS(Uri baseUri, string apiKey)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
        }
        public async Task<List<FeedReading>> GetFeedData(int feedId, DateTime startTime, DateTime endTime, int interval=60)
        {
            var url = string.Format("/feed/data.json?id={0}&start={1}&end={2}&interval={3}", feedId,
                ((DateTimeOffset)startTime).ToUnixTimeSeconds().ToString(), ((DateTimeOffset)endTime).ToUnixTimeSeconds(),
                interval.ToString());
                var request = new HttpRequestMessage()
                {
                    
                    RequestUri = new Uri(this.BaseUri, url),
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("Authorization", string.Format("Bearer {0}", this.ApiKey));

                var response = await Http.Client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            var retVal = new List<FeedReading>();
            var responseBody = (await response.Content.ReadFromJsonAsync<JsonArray>())!;
            foreach(var arr in responseBody) {
                retVal.Add(new FeedReading() {
                    dateTime = DateTimeOffset.FromUnixTimeMilliseconds(arr![0]!.GetValue<long>())!.LocalDateTime,
                    reading = arr![1]!.GetValue<double>(),
                });
            }
            return retVal;
        }

        public async Task<List<FeedReading>> GetDailyFeedData(int feedId, DateTime startTime, DateTime endTime)
        {
            var url = string.Format("/feed/data.json?id={0}&start={1}&end={2}&mode=daily", feedId,
                ((DateTimeOffset)startTime).ToUnixTimeSeconds().ToString(), ((DateTimeOffset)endTime).ToUnixTimeSeconds());
                var request = new HttpRequestMessage()
                {
                    
                    RequestUri = new Uri(this.BaseUri, url),
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("Authorization", string.Format("Bearer {0}", this.ApiKey));

                var response = await Http.Client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            var retVal = new List<FeedReading>();
            var responseBody = (await response.Content.ReadFromJsonAsync<JsonArray>())!;
            foreach(var arr in responseBody) {
                retVal.Add(new FeedReading() {
                    dateTime = DateTimeOffset.FromUnixTimeMilliseconds(arr![0]!.GetValue<long>())!.LocalDateTime,
                    reading = arr![1]!.GetValue<double>(),
                });
            }
            return retVal;
        }
    }
}