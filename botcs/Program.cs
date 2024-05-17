using System.Collections.Concurrent;
using System.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lagrange.Core;
using Lagrange.Core.Common;
using Lagrange.Core.Common.Interface;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Message;
using Lagrange.Core.Message.Entity;

internal class Program
{

    static HttpClientHandler clientHandler = new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
    };
    static HttpClient _client = new(clientHandler);
    private static ConcurrentDictionary<uint, FixedSizedQueue<ChatReqMsg>> chatContexts = new();
    static uint[] admin = new uint[] { 1769712655, 1634204436, 861053440 };
    private static async Task Main(string[] args)
    {
        var config = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync("Config.json"));
        if (config is null)
        {
            Console.WriteLine("Fatal: Config.json not found");
            return;
        }
        await Pixiv.LoginAsync(config.PixivRefreshToken);
        _client.Timeout = TimeSpan.FromSeconds(20);
        ServicePointManager
            .ServerCertificateValidationCallback +=
            (sender, cert, chain, sslPolicyErrors) => true;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.OpenAIKey}");
        await Netease.Login(config.NeteasePhone, config.NeteasePassword);
        // Lagrange.Core.Utility.Sign.
        var deviceInfo = GetDeviceInfo();
        var keyStore = LoadKeystore() ?? new BotKeystore();

        var bot = BotFactory.Create(new BotConfig
        {
            UseIPv6Network = false,
            GetOptimumServer = true,
            AutoReconnect = true,
            Protocol = Protocols.Linux,
            CustomSignProvider = new MacSigner()
        }, deviceInfo, keyStore);

        bot.Invoker.OnBotLogEvent += (context, @event) =>
        {
            Console.WriteLine(@event.ToString());
        };

        bot.Invoker.OnBotOnlineEvent += (context, @event) =>
        {
            Console.WriteLine(@event.ToString());
            SaveKeystore(bot.UpdateKeystore());
        };
        bot.Invoker.OnGroupMessageReceived += async (c, e) =>
        {
            try
            {
                Console.WriteLine(e.Chain.ToString());
                var txt = e.Chain.GetEntity<TextEntity>()?.Text ?? "";
                if (txt.StartsWith("/song"))
                {
                    var songName = txt[5..].Trim();
                    var song = await Netease.GetNetEaseMusicAsync(songName);
                    if (song.Item1 is null)
                    {
                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("未找到歌曲").Build());
                        return;
                    }
                    var bs = await Netease.Mp3ToRecChainAsync(song.Item1!);
                    await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Record(bs, song.Item2).Build());
                }
                else if (txt.StartsWith("/enablechat"))
                {
                    if (admin.Contains(e.Chain.GroupMemberInfo!.Uin))
                    {
                        var reply = MessageBuilder.Group(e.Chain.GroupUin ?? 0);
                        var args = txt[12..];
                        var a = args.Split(" ");
                        var limit = int.Parse(a[0]);
                        var queue = new FixedSizedQueue<ChatReqMsg>(limit,
                        new ChatReqMsg
                        {
                            role = "system",
                            content = string.Join(" ", a[1..])
                        }
                        );

                        chatContexts[e.Chain.GroupUin ?? 0] = queue;
                        reply.Text("已开启聊天");
                        await c.SendMessage(reply.Build());
                        return;
                    }
                    else
                    {
                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("你没有权限").Build());
                        return;
                    }
                }
                else if (txt.StartsWith("/disablechat"))
                {

                    if (admin.Contains(e.Chain.GroupMemberInfo!.Uin))
                    {
                        var reply = MessageBuilder.Group(e.Chain.GroupUin ?? 0);

                        chatContexts.Remove(e.Chain.GroupUin ?? 0, out var _);
                        reply.Text("已关闭聊天");
                        await c.SendMessage(reply.Build());
                        return;
                    }
                    else
                    {
                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("你没有权限").Build());
                        return;
                    }
                }
                else if (txt.StartsWith("/pixiv"))
                {
                    var recs = await Pixiv.GetRecAsync();

                    var a = txt.Split(" ");
                    var limit = 15;
                    if (a.Length > 1)
                    {
                        limit = int.Parse(a[1]);
                    }
                    var reply = MessageBuilder.Group(e.Chain.GroupUin ?? 0);

                    List<MessageBuilder> images = new();
                    ConcurrentDictionary<long, byte[]> imageCache = new();
                    recs = recs.Take(limit).ToArray();
                    Parallel.ForEachAsync(recs, async (item, c) =>
                    {
                        var bs = await Pixiv.GetImageAsync(item.ImageUrls.Large.AbsoluteUri);
                        imageCache[item.Id] = bs;
                    }).Wait();
                    foreach (var item in recs)
                    {
                        var bs = imageCache[item.Id];
                        images.Add(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text($"id: {item.Id}").Image(bs));
                    }
                    reply = reply.MultiMsg(null, images.ToArray());

                    await c.SendMessage(reply.Build());
                    return;
                }
                else if (txt.StartsWith("/star"))
                {
                    var a = txt.Split(" ");
                    if (a.Length < 2)
                    {
                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("参数不足").Build());
                        return;
                    }
                    var id = a[1];
                    await Pixiv.BookmarkAsync(id);
                    await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("收藏成功").Build());
                }
                else if (txt.StartsWith("/unstar"))
                {
                    var a = txt.Split(" ");
                    if (a.Length < 2)
                    {
                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("参数不足").Build());
                        return;
                    }
                    var id = a[1];
                    await Pixiv.UnBookmarkAsync(id);
                    await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("取消收藏成功").Build());
                }
                else if (txt.StartsWith("/bookmarks"))
                {
                    var recs = await Pixiv.GetBookmarksAsync();

                    var a = txt.Split(" ");
                    var limit = 15;
                    var offset = 0;
                    if (a.Length > 1)
                    {
                        limit = int.Parse(a[1]);
                    }
                    if (a.Length > 2)
                    {
                        offset = int.Parse(a[2]);
                    }
                    var reply = MessageBuilder.Group(e.Chain.GroupUin ?? 0);

                    List<MessageBuilder> images = new();
                    ConcurrentDictionary<long, byte[]> imageCache = new();
                    recs = recs.Skip(offset).Take(limit).ToArray();
                    Parallel.ForEachAsync(recs, async (item, c) =>
                    {
                        var bs = await Pixiv.GetImageAsync(item.ImageUrls.Large.AbsoluteUri);
                        imageCache[item.Id] = bs;
                    }).Wait();
                    foreach (var item in recs)
                    {
                        var bs = imageCache[item.Id];
                        images.Add(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text($"id: {item.Id}").Image(bs));
                    }
                    reply = reply.MultiMsg(null, images.ToArray());

                    await c.SendMessage(reply.Build());
                    return;
                }
                else if (txt.StartsWith("/help"))
                {
                    await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text(@"/song [歌名] 搜索歌曲
/pixiv [数量] 获取pixiv推荐
/star [id] 收藏图片
/unstar [id] 取消收藏图片
/bookmarks [数量] [offset] 获取收藏
/enablechat [数量] [初始内容] 开启聊天
/disablechat 关闭聊天").Build());
                    return;
                }

                if (e.Chain.GetEntity<MentionEntity>()?.Uin == bot.BotUin)
                {
                    if (chatContexts.ContainsKey(e.Chain.GroupUin ?? 0))
                    {
                        var content = new List<Dictionary<string, Object>>();
                        foreach (var item in e.Chain)
                        {
                            if (item is TextEntity text)
                            {
                                content.Add(new Dictionary<string, object>{
                                {"type", "text"},
                                {"text", text.Text}
                            });
                            }
                            else if (item is ImageEntity img)
                            {
                                Console.WriteLine($"imgpath: {img.FilePath}");
                                var rp = await _client.GetByteArrayAsync(img.ImageUrl);
                                // convert to image base64
                                var base64 = Convert.ToBase64String(rp);
                                var imageBase64 = "data:image/jpeg;base64," + base64;
                                content.Add(new Dictionary<string, object>{
                                    {"type", "image_url"},
                                    {"image_url", new Dictionary<string, string>{{"url",imageBase64}}}
                                });
                            }
                        }
                        var msgqueue = chatContexts[e.Chain.GroupUin ?? 0];
                        var newChat = new ChatReqMsg
                        {
                            role = "user",
                            content = content
                        };
                        var msgs = msgqueue.ToArray();
                        var newmsgs = msgs.Append(newChat).ToArray();
                        var model = "gpt-4o";
                        var re = await _client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", new ChatReq
                        {
                            messages = newmsgs,
                            model = model
                        });
                        var resp = await re.Content.ReadFromJsonAsync<GPTResp>();
                        if (resp?.choices is null || resp?.choices.Count == 0 || resp?.choices?[0] is null)
                        {
                            await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text("失败：" + await re.Content.ReadAsStringAsync() + "\n" + JsonSerializer.Serialize(content)).Build());
                            return;
                        }
                        var respstr = resp.choices[0].message.content;
                        Console.WriteLine($"resp: {respstr}");
                        msgqueue.Enqueue(newChat);
                        msgqueue.Enqueue(new ChatReqMsg
                        {
                            role = "assistant",
                            content = respstr
                        });

                        await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text(respstr).Build());
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
                await c.SendMessage(MessageBuilder.Group(e.Chain.GroupUin ?? 0).Text(ex.ToString()).Build());
            }
            // MessageBuilder.Group(e.Chain.GroupUin??0).Record()

        };
        // if (await bot.LoginByPassword())
        // {
        //             await Task.Delay(-1);
        // }
        var qrCode = await bot.FetchQrCode();
        if (qrCode != null)
        {
            Console.WriteLine(qrCode.Value.Url);
            await File.WriteAllBytesAsync("qr.png", qrCode.Value.QrCode);
            await bot.LoginByQrCode();
        }

        await Task.Delay(-1);
    }

    public static BotDeviceInfo GetDeviceInfo()
    {
        if (File.Exists("DeviceInfo.json"))
        {
            var info = JsonSerializer.Deserialize<BotDeviceInfo>(File.ReadAllText("DeviceInfo.json"));
            if (info != null) return info;

            info = BotDeviceInfo.GenerateInfo();
            File.WriteAllText("DeviceInfo.json", JsonSerializer.Serialize(info));
            return info;
        }

        var deviceInfo = BotDeviceInfo.GenerateInfo();
        File.WriteAllText("DeviceInfo.json", JsonSerializer.Serialize(deviceInfo));
        return deviceInfo;
    }

    public static void SaveKeystore(BotKeystore keystore) =>
        File.WriteAllText("Keystore.json", JsonSerializer.Serialize(keystore));

    public static BotKeystore? LoadKeystore()
    {
        try
        {
            var text = File.ReadAllText("Keystore.json");
            return JsonSerializer.Deserialize<BotKeystore>(text, new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve
            });
        }
        catch
        {
            return null;
        }
    }
}