using Newtonsoft.Json.Linq;
using System.Text;

namespace DiscordBot
{
    public class Youtube
    {
        public static async Task<string> GetChannelIdAsync(string ChannelURL)
        {
            string url = "https://seostudiotools.com/livewire/message/public.tools.youtube-channel-id";
            string jsonPayload = @$"{{
  ""fingerprint"": {{
    ""id"": ""bqLw0iSFQP2YlvJr0of3"",
    ""name"": ""public.tools.youtube-channel-id"",
    ""locale"": ""en"",
    ""path"": ""youtube-channel-id"",
    ""method"": ""GET"",
    ""v"": ""acj""
  }},
  ""serverMemo"": {{
    ""children"": [],
    ""errors"": [],
    ""htmlHash"": ""c998c29b"",
    ""data"": {{
      ""link"": ""asd"",
      ""data"": null,
      ""recaptcha"": null
    }},
    ""dataMeta"": [],
    ""checksum"": ""45a9c98de3328920f7e3b3921ba5c22ffbc3f47cca0de0fa90a2d31eebdec301""
  }},
  ""updates"": [
    {{
      ""type"": ""syncInput"",
      ""payload"": {{
        ""id"": ""gt61"",
        ""name"": ""link"",
        ""value"": ""{ChannelURL}""
      }}
    }},
    {{
      ""type"": ""callMethod"",
      ""payload"": {{
        ""id"": ""eq4t"",
        ""method"": ""onYoutubeChannelId"",
        ""params"": []
      }}
    }}
  ]
}}
";
            using HttpClient client = new();
            StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();
            JObject Jobject = JObject.Parse(responseContent);
            string channelId = (string)Jobject["serverMemo"]!["data"]!["data"]!;
            if (string.IsNullOrEmpty(channelId))
            {
                throw new("채널을 찾지 못했습니다.");
            }
            else
            {
                return channelId;
            }
        }
        public static async Task<bool> FindSubscribe(string TargetChannel, string AdminChannel, string APIKey)
        {
            string pagetoken = string.Empty;

            while (true)
            {
                string Url = $"https://www.googleapis.com/youtube/v3/subscriptions?part=snippet&channelId={TargetChannel}&key={APIKey}&maxResults=9999&pageToken={pagetoken}";
                using HttpClient client = new();
                try
                {
                    HttpResponseMessage response = await client.GetAsync(Url);
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(responseBody);

                    if (json.ContainsKey("error"))
                    {
                        string errorMessage = (string)json["error"]!["message"]!;
                        throw new($"{errorMessage}");
                    }

                    if (responseBody.Contains(AdminChannel))
                    {
                        return true;
                    }
                    else
                    {

                        if ((json["nextPageToken"]) is null)
                        {
                            return false;
                        }
                        else
                        {
                            pagetoken = (string)(json["nextPageToken"])!;
                        }
                    }
                }
                catch (HttpRequestException error)
                {
                    throw new($"{error.Message}");
                }
            }
        }
    }
}
