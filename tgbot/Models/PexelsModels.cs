using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace tgbot.Models
{
    public class PexelsResponse
    {
        [JsonPropertyName("videos")]
        public List<PexelsVideo> Videos { get; set; }
    }

    public class PexelsVideo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("video_files")]
        public List<VideoFile> VideoFiles { get; set; }
    }

    public class VideoFile
    {
        [JsonPropertyName("link")]
        public string Link { get; set; }

        [JsonPropertyName("quality")]
        public string Quality { get; set; } // "hd" або "sd"
    }
}