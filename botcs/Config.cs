using System.Text.Json.Serialization;

internal class Config
{
    [JsonPropertyName("openai_key")]
    public string OpenAIKey { get; set; }
    [JsonPropertyName("pixiv_refresh_token")]
    public string PixivRefreshToken { get; set; }
    [JsonPropertyName("netease_phone")]
    public string NeteasePhone { get; set; }
    [JsonPropertyName("netease_password")]
    public string NeteasePassword { get; set; }

}
