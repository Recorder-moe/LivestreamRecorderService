using Newtonsoft.Json;
#nullable disable

namespace LivestreamRecorderService.Models;
public class FC2MemberData
{
    [JsonProperty("status")]
    public int? Status { get; set; }

    [JsonProperty("data")]
    public Data Data { get; set; }
}

public class ChannelData
{
    [JsonProperty("channelid")]
    public string Channelid { get; set; }

    [JsonProperty("userid")]
    public string Userid { get; set; }

    [JsonProperty("adult")]
    public int? Adult { get; set; }

    [JsonProperty("twoshot")]
    public int? Twoshot { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("info")]
    public string Info { get; set; }

    [JsonProperty("image")]
    public string Image { get; set; }

    [JsonProperty("login_only")]
    public int? LoginOnly { get; set; }

    [JsonProperty("gift_limit")]
    public int? GiftLimit { get; set; }

    [JsonProperty("gift_list")]
    public List<GiftList> GiftList { get; set; }

    [JsonProperty("comment_limit")]
    public string CommentLimit { get; set; }

    [JsonProperty("tfollow")]
    public int? Tfollow { get; set; }

    [JsonProperty("tname")]
    public string Tname { get; set; }

    [JsonProperty("fee")]
    public int? Fee { get; set; }

    [JsonProperty("amount")]
    public int? Amount { get; set; }

    [JsonProperty("interval")]
    public int? Interval { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("category_name")]
    public string CategoryName { get; set; }

    [JsonProperty("is_official")]
    public int? IsOfficial { get; set; }

    [JsonProperty("is_premium_publisher")]
    public int? IsPremiumPublisher { get; set; }

    [JsonProperty("is_link_share")]
    public int? IsLinkShare { get; set; }

    [JsonProperty("ticketid")]
    public int? Ticketid { get; set; }

    [JsonProperty("is_premium")]
    public int? IsPremium { get; set; }

    [JsonProperty("ticket_price")]
    public int? TicketPrice { get; set; }

    [JsonProperty("ticket_only")]
    public int? TicketOnly { get; set; }

    [JsonProperty("is_app")]
    public int? IsApp { get; set; }

    [JsonProperty("is_video")]
    public int? IsVideo { get; set; }

    [JsonProperty("is_rest")]
    public int? IsRest { get; set; }

    [JsonProperty("count")]
    public int? Count { get; set; }

    [JsonProperty("is_publish")]
    public int? IsPublish { get; set; }

    [JsonProperty("is_limited")]
    public int? IsLimited { get; set; }

    [JsonProperty("start")]
    public long? Start { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("fc2_channel")]
    public Fc2Channel Fc2Channel { get; set; }

    [JsonProperty("control_tag")]
    public string ControlTag { get; set; }

    [JsonProperty("publish_method")]
    public string PublishMethod { get; set; }

    [JsonProperty("video_stereo3d")]
    public int? VideoStereo3d { get; set; }

    [JsonProperty("video_mapping")]
    public int? VideoMapping { get; set; }

    [JsonProperty("video_horizontal_view")]
    public int? VideoHorizontalView { get; set; }
}

public class Data
{
    [JsonProperty("channel_data")]
    public ChannelData ChannelData { get; set; }

    [JsonProperty("profile_data")]
    public ProfileData ProfileData { get; set; }

}

public class Fc2Channel
{
    [JsonProperty("result")]
    public int? Result { get; set; }

    [JsonProperty("userid")]
    public int? Userid { get; set; }

    [JsonProperty("fc2id")]
    public int? Fc2id { get; set; }

    [JsonProperty("adult")]
    public int? Adult { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("images")]
    public List<object> Images { get; set; }
}

public class GiftList
{
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("type")]
    public int? Type { get; set; }

    [JsonProperty("url")]
    public List<string> Url { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("category")]
    public int? Category { get; set; }

    [JsonProperty("amount")]
    public int? Amount { get; set; }
}

public class ProfileData
{
    [JsonProperty("userid")]
    public string Userid { get; set; }

    [JsonProperty("fc2id")]
    public string Fc2id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("info")]
    public string Info { get; set; }

    [JsonProperty("icon")]
    public string Icon { get; set; }

    [JsonProperty("image")]
    public string Image { get; set; }

    [JsonProperty("sex")]
    public string Sex { get; set; }

    [JsonProperty("age")]
    public string Age { get; set; }
}
