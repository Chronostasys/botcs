using Konata.Codec.Audio;
using Konata.Codec.Audio.Codecs;
using NeteaseCloudMusicApi;

internal class Netease
{
    static CloudMusicApi neteaseAPI = new();
    static HttpClient _client = new();

    public static async Task Login(string phone, string pass)
    {
        var queries = new Dictionary<string, object>();
        queries["phone"] = phone;
        queries["password"] = pass;
        await neteaseAPI.RequestAsync(CloudMusicApiProviders.LoginCellphone, queries, false);
    }
    public static async Task<(String?, int)> GetNetEaseMusicAsync(string id)
    {
        var sre = await neteaseAPI.RequestAsync(CloudMusicApiProviders.Cloudsearch, new Dictionary<string, object> { ["keywords"] = id }, false);
        // Console.WriteLine(sre);
        var first = sre["result"]?["songs"]?[0]?["id"]?.ToString();
        if (first is null)
            return (null, 0);
        var ure = await neteaseAPI.RequestAsync(CloudMusicApiProviders.SongUrlV1, new Dictionary<string, object> { ["id"] = first }, false);
        // Console.WriteLine(ure);
        var time = int.Parse(ure["data"]?[0]?["time"]?.ToString() ?? "30000") / 1000;
        Console.WriteLine(time);
        var url = ure["data"]?[0]?["url"]?.ToString();
        return (url, time);
    }
    public static async Task<byte[]> Mp3ToRecChainAsync(string mp3url)
    {
        using var mp3stream = await _client.GetStreamAsync(mp3url);
        var slkstream = new MemoryStream();

        // Create audio pipeline
        using var mp3pipeline = new AudioPipeline
        {
            // Mp3 decoder stream
            new  Mp3Codec.Decoder(mp3stream),
            new AudioResampler(new AudioInfo(AudioFormat.Signed16Bit, AudioChannel.Mono, 24000)),
            new SilkV3Codec.Encoder(),
            slkstream
        };
        var succ = await mp3pipeline.Start();
        if (!succ)
        {
            throw new Exception("slk encode failed");
        }
        Console.WriteLine($"slkstream length: {slkstream.Length}");
        slkstream.Position = 0;
        return slkstream.GetBuffer()[..(int)slkstream.Length];
    }
}
