using PixivCS;

namespace botcs.Test;

public class UnitTest
{
    [Fact]
    public async Task TestPixiv()
    {
        var api = new PixivAppAPI();
        var re = await api.AuthAsync("1WRRkxi2fNjvrY4ZcMFbyw5sOxnMf2uJojd5UjsCs7w");
        var rec = await api.GetIllustRecommendedAsync();
        var uris = new List<Uri>();
        foreach (var item in rec.Illusts)
        {
            if ( item.Tags.Select(t => t.Name).Contains("R-18") || item.Tags.Select(t => t.Name).Contains("R-18G"))
            {
                uris.Add(item.ImageUrls.Large);
                await api.DownloadAsync(item.ImageUrls.Large.AbsoluteUri, item.Id.ToString() + ".jpg");   
            }
        }

        Console.WriteLine(rec);
    }
}