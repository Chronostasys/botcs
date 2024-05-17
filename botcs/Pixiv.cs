using PixivCS;
using PixivCS.Objects;

internal class Pixiv
{
    static PixivAppAPI api = new();
    public static async Task LoginAsync(string refreshToken)
    {
        var re = await api.AuthAsync(refreshToken);
    }
    public static async Task<UserPreviewIllust[]> GetRecAsync()
    {
        var rec = await api.GetIllustRecommendedAsync();
        return rec.Illusts;
    }
    public static async Task<UserPreviewIllust[]> GetBookmarksAsync()
    {
        var bookmarks = await api.GetUserBookmarksIllustAsync(api.UserID);
        return bookmarks.Illusts;
    }

    public static async Task BookmarkAsync(string id)
    {
        await api.PostIllustBookmarkAddAsync(id);
    }

    public static async Task UnBookmarkAsync(string id)
    {
        await api.PostIllustBookmarkDeleteAsync(id);
    }
    public static async Task<byte[]> GetImageAsync(string url)
    {
        var re = await api.DownloadBytesAsync(url);
        return re;
    }
}
