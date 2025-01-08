using System.Text.Json.Serialization;
#nullable disable

namespace LivestreamRecorderService.Models;
public class FC2MemberData
{
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("data")]
    public Data Data { get; set; }
}

public class ChannelData
{
    [JsonPropertyName("channelid")]
    public string Channelid { get; set; }

    [JsonPropertyName("userid")]
    public string Userid { get; set; }

    [JsonPropertyName("adult")]
    public int? Adult { get; set; }

    [JsonPropertyName("twoshot")]
    public int? Twoshot { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("info")]
    public string Info { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("login_only")]
    public int? LoginOnly { get; set; }

    [JsonPropertyName("gift_limit")]
    public int? GiftLimit { get; set; }

    [JsonPropertyName("gift_list")]
    public List<GiftList> GiftList { get; set; }

    [JsonPropertyName("comment_limit")]
    public string CommentLimit { get; set; }

    [JsonPropertyName("tfollow")]
    public int? Tfollow { get; set; }

    [JsonPropertyName("tname")]
    public string Tname { get; set; }

    [JsonPropertyName("fee")]
    public int? Fee { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }

    [JsonPropertyName("interval")]
    public int? Interval { get; set; }

    [JsonPropertyName("category")]
    public int Category { get; set; }

    [JsonPropertyName("category_name")]
    public string CategoryName { get; set; }

    [JsonPropertyName("is_official")]
    public int? IsOfficial { get; set; }

    [JsonPropertyName("is_premium_publisher")]
    public int? IsPremiumPublisher { get; set; }

    [JsonPropertyName("is_link_share")]
    public int? IsLinkShare { get; set; }

    [JsonPropertyName("ticketid")]
    public int? Ticketid { get; set; }

    [JsonPropertyName("is_premium")]
    public int? IsPremium { get; set; }

    [JsonPropertyName("ticket_price")]
    public int? TicketPrice { get; set; }

    [JsonPropertyName("ticket_only")]
    public int? TicketOnly { get; set; }

    [JsonPropertyName("is_app")]
    public int? IsApp { get; set; }

    [JsonPropertyName("is_video")]
    public int? IsVideo { get; set; }

    [JsonPropertyName("is_rest")]
    public int? IsRest { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("is_publish")]
    public int? IsPublish { get; set; }

    [JsonPropertyName("is_limited")]
    public int? IsLimited { get; set; }

    [JsonPropertyName("start")]
    public long? Start { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("fc2_channel")]
    public Fc2Channel Fc2Channel { get; set; }

    [JsonPropertyName("control_tag")]
    public string ControlTag { get; set; }

    [JsonPropertyName("publish_method")]
    public string PublishMethod { get; set; }

    //[JsonPropertyName("video_stereo3d")]
    //public object VideoStereo3d { get; set; }

    //[JsonPropertyName("video_mapping")]
    //public object VideoMapping { get; set; }

    //[JsonPropertyName("video_horizontal_view")]
    //public object VideoHorizontalView { get; set; }
}

public class Data
{
    [JsonPropertyName("channel_data")]
    public ChannelData ChannelData { get; set; }

    [JsonPropertyName("profile_data")]
    public ProfileData ProfileData { get; set; }
}

public class Fc2Channel
{
    [JsonPropertyName("result")]
    public int? Result { get; set; }

    [JsonPropertyName("userid")]
    public int? Userid { get; set; }

    [JsonPropertyName("fc2id")]
    public int? Fc2id { get; set; }

    [JsonPropertyName("adult")]
    public int? Adult { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("images")]
    public List<object> Images { get; set; }
}

public class GiftList
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonPropertyName("url")]
    public List<string> Url { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("category")]
    public int? Category { get; set; }

    [JsonPropertyName("amount")]
    public int? Amount { get; set; }
}

public class ProfileData
{
    [JsonPropertyName("userid")]
    public string Userid { get; set; }

    [JsonPropertyName("fc2id")]
    public string Fc2id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("info")]
    public string Info { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("sex")]
    public string Sex { get; set; }

    [JsonPropertyName("age")]
    public string Age { get; set; }
}
