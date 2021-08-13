﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UtilmeSdpTransform;

namespace Utilme.SdpTransform
{
    public static class ModelExtensions
    {

        public static Sdp ToSdp(this string str)
        {
            Sdp sdp = new();

            var tokens = str.Split(new string[] { Constants.CRLF }, StringSplitOptions.RemoveEmptyEntries);
            var idx = 0;
            
            // Session parameters.
            foreach (var token in tokens)
            {
                if (token.StartsWith(Sdp.ProtocolVersionIndicator))
                    sdp.ProtocolVersion = token.ToProtocolVersion();
                else if (token.StartsWith(Sdp.OriginIndicator))
                    sdp.Origin = token.ToOrigin();
                else if (token.StartsWith(Sdp.SessionNameIndicator))
                    sdp.SessionName = token.ToSessionName();
                else if (token.StartsWith(Sdp.SessionInformationIndicator))
                    sdp.SessionInformation = token.ToSessionInformation();
                else if (token.StartsWith(Sdp.UriIndicator))
                    sdp.Uri = token.ToUri();
                else if (token.StartsWith(Sdp.EmailAddressIndicator))
                    sdp.EmailAddresses = token.ToEmailAddresses();
                else if (token.StartsWith(Sdp.PhoneNumberIndicator))
                    sdp.PhoneNumbers = token.ToPhoneNumbers();
                else if (token.StartsWith(Sdp.ConnectionDataIndicator))
                    sdp.ConnectionData = token.ToConnectionData();
                else if (token.StartsWith(Sdp.BandwidthIndicator))
                {
                    sdp.Bandwidths ??= new List<Bandwidth>();
                    sdp.Bandwidths.Add(token.ToBandwidth());
                }
                else if (token.StartsWith(Sdp.TimingIndicator))
                {
                    sdp.Timings ??= new List<Timing>();
                    sdp.Timings.Add(token.ToTiming());
                }
                else if (token.StartsWith(Sdp.RepeatTimeIndicator))
                {
                    sdp.RepeatTimes ??= new List<RepeatTime>();
                    sdp.RepeatTimes.Add(token.ToRepeatTime());
                }
                else if (token.StartsWith(Sdp.TimeZoneIndicator))
                    sdp.TimeZones = token.ToTimeZones();


                else if (token.StartsWith(Sdp.MediaDescriptionIndicator))
                    break;


                idx++;
            }

            return sdp;
        }

        public static string ToText(this Sdp sdp)
        {
            throw new NotImplementedException();
        }

        public static int ToProtocolVersion(this string str)
        {
            var token = str
                 .Replace(Sdp.ProtocolVersionIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);
            return int.Parse(token);
        }

        public static Origin ToOrigin(this string str)
        {
            var tokens = str
                 .Replace(Sdp.OriginIndicator, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Origin
            {
                UserName = tokens[0],
                SessionId = ulong.Parse(tokens[1]),
                SessionVersion = uint.Parse(tokens[2]),
                NetType = tokens[3].EnumFromDisplayName<NetType>(),
                AddrType = tokens[4].EnumFromDisplayName<AddrType>(),
                UnicastAddress = tokens[5]
            };
        }

        public static string ToSessionName(this string str)
        {
            var token = str
                 .Replace(Sdp.SessionNameIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);
            return token;
        }

        public static string ToSessionInformation(this string str)
        {
            var token = str
                 .Replace(Sdp.SessionInformationIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);
            return token;
        }

        public static Uri ToUri(this string str)
        {
            var token = str
                 .Replace(Sdp.UriIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);
            return new Uri(token);
        }

        public static List<string> ToEmailAddresses(this string str)
        {
            var token = str
                 .Replace(Sdp.EmailAddressIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);

            List<string> emails = new();

            // Email formats:
            //  x.y@z.org
            //  x.y@z.org (Name Surname)
            //  Name Surname <x.y@z.org>
            var groupA = token.Split(new char[] { ')', '>' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var a in groupA)
            {
                if (a.Contains("("))
                {
                    var groupB = a.Split(new char[] { '(' }, StringSplitOptions.RemoveEmptyEntries);
                    var id = groupB[1];
                    var groupC = groupB[0].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var emailWithId = $"{groupC[groupC.Length - 1]} ({id})";
                    if (groupC.Length > 1)
                    {
                        var groupD = groupC.Take(groupC.Length - 1).ToArray();
                        foreach (var d in groupD)
                            emails.Add(d);
                    }
                    emails.Add(emailWithId);
                }
                else if (a.Contains("<"))
                {
                    var groupB = a.Split(new char[] { '<' }, StringSplitOptions.RemoveEmptyEntries);
                    var groupC = groupB[0].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var numPlainEmails = groupC.Where(g => g.Contains('@')).Count();
                    var plainEmails = groupC.Take(numPlainEmails);
                    var id = string.Join(" ", 
                        groupC.Skip(numPlainEmails).Take(groupC.Count() - numPlainEmails).ToArray());
                    var email = groupB[1];
                    var emailWithId = $"{id} <{email}>";
                    foreach (var plainEmail in plainEmails)
                        emails.Add(plainEmail);
                    emails.Add(emailWithId);
                }
                else
                {
                    var groupB = a.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var b in groupB)
                        emails.Add(b);
                }
            }

            return emails;
        }

        public static List<string> ToPhoneNumbers(this string str)
        {
            var token = str
                 .Replace(Sdp.PhoneNumberIndicator, string.Empty)
                 .Replace(Constants.CRLF, string.Empty);

            List<string> phones = new();

            // Phone formats:
            //  +x.y@z.org
            //  x.y@z.org (Name Surname)
            //  Name Surname <x.y@z.org>
            var groupA = token.Split(new char[] { ')', '>' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var a in groupA)
            {
                if (a.Contains("("))
                {
                    var groupB = a.Split(new char[] { '(' }, StringSplitOptions.RemoveEmptyEntries);
                    var id = groupB[1];
                    var groupC = groupB[0].Trim().Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                    var phoneWithId = $"+{groupC[groupC.Length - 1].Trim()} ({id})";
                    if (groupC.Length > 1)
                    {
                        var groupD = groupC.Take(groupC.Length - 1).ToArray();
                        foreach (var d in groupD)
                            phones.Add($"+{d.Trim()}");
                    }
                    phones.Add(phoneWithId);
                }
                else if (a.Contains("<"))
                {
                    var groupB = a.Split(new char[] { '<' }, StringSplitOptions.RemoveEmptyEntries);
                    var groupC = Regex.Matches(groupB[0].Trim(), @"\+[\d -]+");
                    var numPlainPhones = groupC.Count - 1;
                    var lenPhones = 0;
//                    var plainPhones = new string[numPlainPhones];
                    for (var i = 0; i < numPlainPhones; i++)
                    {
                        lenPhones += groupC[i].Length;
                        phones.Add(groupC[i].Value.Trim());
                    }
                    lenPhones += groupC[numPlainPhones].Length;
                    var id = groupB[0].Trim().Substring(lenPhones);
                    var phone = groupC[1].Value.Trim();
                    var phoneWithId = $"{id} <{phone.Trim()}>";
                    phones.Add(phoneWithId);
                }
                else
                {
                    var groupB = a.Trim().Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var b in groupB)
                        phones.Add($"+{b.Trim()}");
                }
            }

            return phones;
        }

        public static ConnectionData ToConnectionData(this string str)
        {
            var tokens = str
                 .Replace(Sdp.ConnectionDataIndicator, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new ConnectionData
            {
                NetType = tokens[0].EnumFromDisplayName<NetType>(),
                AddrType = tokens[1].EnumFromDisplayName<AddrType>(),
                ConnectionAddress = tokens[2]
            };
        }

        public static Bandwidth ToBandwidth(this string str)
        {
            var tokens = str
                 .Replace(Sdp.BandwidthIndicator, string.Empty)
                 .Split(new char[] { ' ', ':', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Bandwidth
            {
                Type = tokens[0].EnumFromDisplayName<BandwidthType>(),
                Value = int.Parse(tokens[1])
            };
        }

        public static Timing ToTiming(this string str)
        {
            var tokens = str
                 .Replace(Sdp.TimingIndicator, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Timing
            {
                StartTime = new DateTime(1900, 1, 1) + TimeSpan.FromSeconds(double.Parse(tokens[0])),
                StopTime = new DateTime(1900, 1, 1) + TimeSpan.FromSeconds(double.Parse(tokens[1]))
            };
        }

        public static RepeatTime ToRepeatTime(this string str)
        {
            var tokens = str
                 .Replace(Sdp.RepeatTimeIndicator, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var offsetsStr = tokens.Skip(2);
            var offsets = new List<TimeSpan>();
            foreach (var offsetStr in offsetsStr)
                offsets.Add(TimeSpan.FromSeconds(Regex.IsMatch(offsetStr, @"[dhms]$") ?
                    ToSeconds(offsetStr) : double.Parse(offsetStr)));

            return new RepeatTime
            {
                RepeatInterval = TimeSpan.FromSeconds(Regex.IsMatch(tokens[0], @"[dhms]$") ?
                    ToSeconds(tokens[0]) : double.Parse(tokens[0])),
                ActiveDuration = TimeSpan.FromSeconds(Regex.IsMatch(tokens[1], @"[dhms]$") ?
                    ToSeconds(tokens[1]) : double.Parse(tokens[1])),
                OffsetsFromStartTime = offsets
            };
        }

        public static List<TimeZone> ToTimeZones(this string str)
        {
            var tokens = str
                 .Replace(Sdp.TimeZoneIndicator, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length % 2 != 0)
                throw new FormatException("Timezones should be specified in pairs");
            var pairs = tokens
                .Select((token, idx) => new { idx, token })
                .GroupBy(p => p.idx / 2, p => p.token);

            List<TimeZone> timeZones = new();

            foreach (var pair in pairs)
            {
                var array = pair.ToArray();
                timeZones.Add(new TimeZone 
                { 
                    AdjustmentTime = new DateTime(1900, 1, 1) + TimeSpan.FromSeconds(double.Parse(array[0])),
                    Offset = TimeSpan.FromSeconds(Regex.IsMatch(array[1], @"[dhms]$") ?
                        ToSeconds(array[1]) : double.Parse(array[1]))
                });
            }

            return timeZones;
        }





        public static Candidate ToCandidate(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Candidate.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Candidate
            {
                Foundation = tokens[0],
                ComponentId = int.Parse(tokens[1]),
                Transport = tokens[2].EnumFromDisplayName<CandidateTransport>(),// (CandidateTransport)Enum.Parse(typeof(CandidateTransport), tokens[2], true),
                Priority = int.Parse(tokens[3]),
                ConnectionAddress = tokens[4],
                Port = int.Parse(tokens[5]),
                Type = tokens[7].EnumFromDisplayName<CandidateType>(), //(CandidateType)Enum.Parse(typeof(CandidateType), tokens[7], true),
                RelAddr = tokens[9],
                RelPort = int.Parse(tokens[11])
            };
        }

        public static IceUfrag ToIceUfrag(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(IceUfrag.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new IceUfrag
            {
                Ufrag = tokens[0]
            };
        }

        public static IcePwd ToIcePwd(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(IcePwd.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new IcePwd
            {
                Password = tokens[0]
            };
        }

        public static IceOptions ToIceOptions(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(IceOptions.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var tags = new string[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
                tags[i] = tokens[i];
            return new IceOptions
            {
                Tags = tags
            };
        }

        public static Mid ToMid(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Mid.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Mid
            {
                Id = tokens[0]
            };
        }

        public static MsidSemantic ToMsidSemantic(this string str)
        {
            var tokens = str
                .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                .Replace(MsidSemantic.Name, string.Empty)
                .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var idList = new string[tokens.Length -1];
            for (int i = 0; i < idList.Length; i++)
                idList[i] = tokens[i + 1];
            return new MsidSemantic
            {
                Token = tokens[0],
                IdList = idList
            };
        }

        public static Fingerprint ToFingerprint(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Fingerprint.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Fingerprint
            {
                HashFunction = tokens[0].EnumFromDisplayName<HashFunction>(), //(HashFunction)Enum.Parse(typeof(HashFunction), tokens[0], true),
                HashValue = HexadecimalStringToByteArray(tokens[1].Replace(":", string.Empty))
            };

        }

        public static Group ToGroup(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Group.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var groupTokens = new string[tokens.Length - 1];
            for (int i = 1; i < tokens.Length; i++)
                groupTokens[i - 1] = tokens[i];
            return new Group
            {
                Type = tokens[0],
                Tokens = groupTokens
            };
        }

        public static Msid ToMsid(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Msid.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Msid
            {
                Id = tokens[0],
                AppData = tokens[1]
            };
        }

        public static Ssrc ToSsrc(this string str)
        {
            var tokens = str
                .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                .Replace(Ssrc.Name, string.Empty)
                .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var attributeAndValue = tokens[1].Split(':');
            return new Ssrc
            {
                Id = uint.Parse(tokens[0]),
                Attribute = attributeAndValue[0],
                Value = attributeAndValue.Length > 1 ? attributeAndValue[1] : null
             };
        }

        public static SsrcGroup ToSsrcGroup(this string str)
        {
            var tokens = str
                .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                .Replace(SsrcGroup.Name, string.Empty)
                .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var ssrcIds = new string[tokens.Length - 1];
            for (int i = 1; i < tokens.Length; i++)
                ssrcIds[i - 1] = tokens[i];
            return new SsrcGroup
            {
                Semantics = tokens[0],
                SsrcIds = ssrcIds
            };
        }

        public static Rid ToRid(this string str)
        {
            var tokens = str
                .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                .Replace(Rid.Name, string.Empty)
                .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var subTokens = tokens[2].Split(';');
            var fmtList = subTokens.Where(st => st.StartsWith("pt=")).ToArray();
            var restrictions = subTokens.Skip(fmtList.Length).Take(subTokens.Length - fmtList.Length).ToArray();
            return new Rid
            {
                Id = tokens[0],
                Direction = tokens[1].EnumFromDisplayName<RidDirection>(), //(RidDirection)Enum.Parse(typeof(RidDirection), tokens[1], true),
                FmtList = fmtList,
                Restrictions = restrictions
            };
        }

        public static Rtpmap ToRtpmap(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Rtpmap.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var subTokens = tokens[1].Split('/');
            return new Rtpmap
            {
                PayloadType = int.Parse(tokens[0]),
                EncodingName = subTokens[0],
                ClockRate = int.Parse(subTokens[1]),
                Channels = subTokens.Length > 2 ? int.Parse(subTokens[2]) : null
            };
        }

        public static Fmtp ToFmtp(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(Fmtp.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Fmtp
            {
                PayloadType = int.Parse(tokens[0]),
                Value = tokens[1],
            };
        }

        // Dictionary
        //  {
        //      { "level-asymmetry-allowed", "1" },
        //      { "packetization-mode", "0" },
        //      { "profile-level-id", "42e01f" }
        //  }
        // Fmtp
        // {
        //  PayloadType = 108,
        //  Value = "level-asymmetry-allowed=1;packetization-mode=0;profile-level-id=42e01f"
        // }
        // a=fmtp:108 level-asymmetry-allowed=1;packetization-mode=0;profile-level-id=42e01f
        public static Fmtp ToFmtp(this Dictionary<string, object/*string*/> dictionary, int payloadType)
        {
            Fmtp fmtp = new()
            {
                PayloadType = payloadType,
                Value = string.Empty
            };

            foreach (var key in dictionary.Keys)
            {
                if (!string.IsNullOrEmpty(fmtp.Value))
                    fmtp.Value += ";";
                fmtp.Value += $"{key}={dictionary[key]}";
            }

            return fmtp;
        }


        public static RtcpFb ToRtcpFb(this string str)
        {
            var tokens = str
                 .Replace(SdpSerializer.AttributeCharacter, string.Empty)
                 .Replace(RtcpFb.Name, string.Empty)
                 .Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new RtcpFb
            {
                PayloadType = int.Parse(tokens[0]),
                Type = tokens[1],
                SubType = tokens.Length == 3 ? tokens[2] : null
            };
        }

        public static string ToAttributeString(this Candidate candidate, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Candidate.Name)
                .Append(candidate.Foundation)
                .Append(" ")
                .Append(candidate.ComponentId.ToString())
                .Append(" ")
                .Append(candidate.Transport.DisplayName())
                .Append(" ")
                .Append(candidate.Priority.ToString())
                .Append(" ")
                .Append(candidate.ConnectionAddress)
                .Append(" ")
                .Append(Candidate.Typ)
                .Append(" ")
                .Append(candidate.Type.DisplayName())
                .Append(" ")
                .Append(Candidate.Raddr)
                .Append(" ")
                .Append(candidate.RelAddr)
                .Append(" ")
                .Append(Candidate.Rport)
                .Append(" ")
                .Append(candidate.RelPort)
                .Append(SdpSerializer.CRLF);
            
            return sb.ToString();
        }

        public static string ToAttributeString(this IceUfrag iceUfrag, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(IceUfrag.Name)
                .Append(iceUfrag.Ufrag)
                .Append(SdpSerializer.CRLF);
            return sb.ToString();
        }
        
        public static string ToAttributeString(this IcePwd icePwd, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(IcePwd.Name)
                .Append(icePwd.Password)
                .Append(SdpSerializer.CRLF);
            
            return sb.ToString();
        }

        public static string ToAttributeString(this IceOptions iceOptions, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(IceOptions.Name)
                .Append("");
            for (int i=0; i<iceOptions.Tags.Length; i++)
            {
                sb.Append(iceOptions.Tags[i]);
                if (i != iceOptions.Tags.Length - 1)
                    sb.Append(" ");
            }
            sb.Append(SdpSerializer.CRLF);
            
            return sb.ToString();
        }

        public static string ToAttributeString(this Mid mid, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Mid.Name)
                .Append(mid.Id)
                .Append(SdpSerializer.CRLF);
            
            return sb.ToString();
        }

        public static string ToAttributeString(this MsidSemantic msidSemantic, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(MsidSemantic.Name)
                .Append(msidSemantic.Token)
                .Append(" ");
            for (int i= 0; i < msidSemantic.IdList.Length; i++)
            {
                sb.Append(msidSemantic.IdList[i]);
                if (i != msidSemantic.IdList.Length -1)
                    sb.Append(" ");
                sb.Append(" ");
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this Fingerprint fingerprint, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Fingerprint.Name)
                .Append(fingerprint.HashFunction.DisplayName())
                .Append(" ")
                .Append(BitConverter.ToString(fingerprint.HashValue).Replace("-",":"))
                .Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this Group group, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(MsidSemantic.Name)
                .Append(group.Type)
                .Append(" ");
            for (int i = 0; i < group.Tokens.Length; i++)
            {
                sb.Append(group.Tokens[i]);
                if (i != group.Tokens.Length - 1)
                    sb.Append(" ");
            }
            sb.Append(SdpSerializer.CRLF);
            
            return sb.ToString();
        }

        public static string ToAttributeString(this Msid msid, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Msid.Name)
                .Append(msid.Id)
                .Append(" ")
                .Append(msid.AppData)
                .Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this Ssrc ssrc, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Ssrc.Name)
                .Append(ssrc.Id.ToString())
                .Append(" ")
                .Append(ssrc.Attribute);
            if (ssrc.Value is not null)
            {
                sb
                    .Append(":")
                    .Append(ssrc.Value);
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this SsrcGroup ssrcGroup, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(SsrcGroup.Name)
                .Append(ssrcGroup.Semantics)
                .Append(" ");
            for (int i = 0; i < ssrcGroup.SsrcIds.Length; i++)
            {
                sb.Append(ssrcGroup.SsrcIds[i]);
                if (i != ssrcGroup.SsrcIds.Length - 1)
                    sb.Append(" ");
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this Rid rid, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Rid.Name)
                .Append(rid.Id)
                .Append(" ")
                .Append(rid.Direction.DisplayName());
            for (var i = 0; i < rid.FmtList.Length; i++)
            {
                sb.Append(rid.FmtList[i]);
                sb.Append(";");
            }
            for (var i = 0; i < rid.Restrictions.Length; i++)
            {
                sb.Append(rid.Restrictions[i]);
                if (i != rid.Restrictions.Length - 1)
                    sb.Append(";");
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        public static string ToAttributeString(this Rtpmap rtpmap, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Rtpmap.Name)
                .Append(rtpmap.PayloadType.ToString())
                .Append(" ")
                .Append(rtpmap.EncodingName)
                .Append("/")
                .Append(rtpmap.ClockRate.ToString());
            if (rtpmap.Channels.HasValue)
            {
                sb
                    .Append("/")
                    .Append(rtpmap.Channels);
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();

        }

        public static string ToAttributeString(this Fmtp fmtp, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(Fmtp.Name)
                .Append(fmtp.PayloadType.ToString())
                .Append(" ")
                .Append(fmtp.Value)
                .Append(SdpSerializer.CRLF);

            return sb.ToString();
        }

        // a=fmtp:108 level-asymmetry-allowed=1;packetization-mode=0;profile-level-id=42e01f
        // Fmtp
        // {
        //  PayloadType = 108,
        //  Value = "level-asymmetry-allowed=1;packetization-mode=0;profile-level-id=42e01f"
        // }
        // Dictionary
        //  {
        //      { "level-asymmetry-allowed", "1" },
        //      { "packetization-mode", "0" },
        //      { "profile-level-id", "42e01f" }
        //  }

        public static Dictionary<string, object/*string*/> ToDictionary(this Fmtp fmtp)
        {
            Dictionary<string, object/*string*/> dictionary = new();

            var tokens = fmtp.Value.Split(';');
            foreach (var token in tokens)
            {
                var subTokens = token.Split('=');
                if (int.TryParse(subTokens[1], out int n))
                    dictionary.Add(subTokens[0], n);
                else
                dictionary.Add(subTokens[0], subTokens[1]);
            }
            return dictionary;
        }

        public static string ToAttributeString(this RtcpFb rtcpFb, bool withAttributeCharacter = false)
        {
            StringBuilder sb = new();
            if (withAttributeCharacter)
                sb.Append(SdpSerializer.AttributeCharacter);
            sb
                .Append(RtcpFb.Name)
                .Append(rtcpFb.PayloadType.ToString())
                .Append(" ")
                .Append(rtcpFb.Type)
                .Append(SdpSerializer.CRLF);
            if (rtcpFb.SubType is not null)
            {
                sb
                    .Append(" ")
                    .Append(rtcpFb.SubType);
            }
            sb.Append(SdpSerializer.CRLF);

            return sb.ToString();

        }

        // Utility method.
        public static byte[] HexadecimalStringToByteArray(String hexadecimalString)
        {
            int length = hexadecimalString.Length;
            byte[] byteArray = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                byteArray[i / 2] = Convert.ToByte(hexadecimalString.Substring(i, 2), 16);
            }
            return byteArray;
        }

        public static long ToSeconds(this string str)
        {
            // Converts strings ending with the following letters to seconds.
            //  <digits>d - days (86400 seconds)
            //  <digits>h - hours (3600 seconds)
            //  <digits>m - minutes (60 seconds)
            //  <digits>s - seconds 
            if (str.EndsWith("d"))
                return long.Parse(str.TrimEnd('d')) * 86400;
            else if (str.EndsWith("h"))
                return long.Parse(str.TrimEnd('h')) * 3600;
            else if (str.EndsWith("m"))
                return long.Parse(str.TrimEnd('h')) * 3600;
            else if (str.EndsWith("s"))
                return long.Parse(str.TrimEnd('h')) * 3600;
            else
                throw new NotSupportedException();
        }
    }
}
