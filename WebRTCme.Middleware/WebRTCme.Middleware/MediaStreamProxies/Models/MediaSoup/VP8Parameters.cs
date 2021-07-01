﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WebRTCme.Middleware.MediaStreamProxies.Models.MediaSoup
{
    class VP8Parameters
    {
        [JsonPropertyName("profile-id")]
        public int? ProfileId { get; init; }

        [JsonPropertyName("x-google-start-bitrate")]
        public int? XGoogleStartBitrate { get; init; }

        [JsonPropertyName("x-google-max-bitrate")]
        public int? XGoogleMaxBitrate { get; init; }

        [JsonPropertyName("x-google-min-bitrate")]
        public int? XGoogleMinBitrate { get; init; }

    }
}
