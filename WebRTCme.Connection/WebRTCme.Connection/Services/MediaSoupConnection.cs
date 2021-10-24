﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Utilme;
using WebRTCme.Connection.MediaSoup;
using WebRTCme.Connection.MediaSoup.Proxy;
using WebRTCme.Connection.MediaSoup.Proxy.Client;
using WebRTCme.Connection.MediaSoup.Proxy.Enums;
using WebRTCme.Connection.MediaSoup.Proxy.Models;
using Xamarin.Essentials;

namespace WebRTCme.Connection.Services
{
    class MediaSoupConnection : IConnection, IMediaSoupServerNotify
    {

int cnt;
IMediaStream remMedia; 


        readonly IConfiguration _configuration;
        readonly IMediaSoupServerApi _mediaSoupServerApi;
        readonly ILogger<MediaSoupConnection> _logger;
        readonly IWebRtc _webRtc;
        readonly IJSRuntime _jsRuntime;
        readonly MediaSoup.Proxy.Models.Device _device;

        ConnectionContext _connectionContext;

        MediaSoup.Proxy.Client.Device _mediaSoupDevice;
        Transport _sendTransport;
        Transport _recvTransport;
        string _displayName;

        bool _produce;
        bool _consume;
        bool _useDataChannel;

        Dictionary<string, Consumer> _consumers = new();
        Dictionary<string, DataConsumer> _dataConsumers = new();
        Producer _micProducer;
        Producer _webcamProducer;
        DataProducer _chatDataProducer;
        DataProducer _botDataProducer;
        Producer _shareProducer;
        Dictionary<string, PeerParameters> _peers = new();

        public MediaSoupConnection(IConfiguration configuration, 
            IMediaSoupServerApi mediaSoupServerApi,
            ILogger<MediaSoupConnection> logger, IWebRtc webRtc, IJSRuntime jsRuntime = null)
        {
            _configuration = configuration;
            _mediaSoupServerApi = mediaSoupServerApi;
            _logger = logger;
            _webRtc = webRtc;
            _jsRuntime = jsRuntime;

            _device = GetDevice();
        }


        public IObservable<PeerResponse> ConnectionRequest(UserContext userContext)
        {
            return Observable.Create<PeerResponse>(async observer =>
            {
                var guid = Guid.NewGuid();
                var forceTcp = _configuration.GetValue<bool>("MediaSoupServer:ForceTcp");
                _produce = _configuration.GetValue<bool>("MediaSoupServer:Produce");
                _consume = _configuration.GetValue<bool>("MediaSoupServer:Consume");
                _useDataChannel = _configuration.GetValue<bool>("MediaSoupServer:UseDataChannel");
                var forceH264 = _configuration.GetValue<bool>("MediaSoupServer:ForceH264");
                var forceVP9 = _configuration.GetValue<bool>("MediaSoupServer:ForceVP9");
                var useSimulcast = _configuration.GetValue<bool>("MediaSoupServer:UseSimulcast");
                var useSharingSimulcast = _configuration.GetValue<bool>("MediaSoupServer:UseSharingSimulcast");
                var audioOnly = _configuration.GetValue<bool>("MediaSoupServer:AudioOnly");
                var e2eKey = _configuration.GetValue<string>("MediaSoupServer:E2eKey");

                _displayName = userContext.Name;

                _connectionContext = new ConnectionContext
                {
                    UserContext = userContext,
                    Observer = observer
                };


                try
                {
                    _mediaSoupServerApi.NotifyEventAsync += OnNotifyAsync;
                    _mediaSoupServerApi.RequestEventAsync += OnRequestAsync;

                    await _mediaSoupServerApi.ConnectAsync(guid, userContext.Name, userContext.Room);

                    _mediaSoupDevice = new MediaSoup.Proxy.Client.Device();

                    var routerRtpCapabilities = (RtpCapabilities)ParseResponse(MethodName.GetRouterRtpCapabilities,
                        await _mediaSoupServerApi.ApiAsync(MethodName.GetRouterRtpCapabilities));
                    await _mediaSoupDevice.LoadAsync(routerRtpCapabilities);


                    // Create mediasoup Transport for sending (unless we don't want to produce).
                    if (_produce)
                    {
                        var transportInfo = (TransportInfo)ParseResponse(MethodName.CreateWebRtcTransport,
                            await _mediaSoupServerApi.ApiAsync(MethodName.CreateWebRtcTransport,
                                new WebRtcTransportCreateRequest
                                {
                                    ForceTcp = forceTcp,
                                    Producing = true,
                                    Consuming = false,
                                    SctpCapabilities = _useDataChannel ? _mediaSoupDevice.SctpCapabilities : null
                                }));

                        _sendTransport = _mediaSoupDevice.CreateSendTransport(new TransportOptions 
                        {
                            Id = transportInfo.Id,
                            IceParameters = transportInfo.IceParameters,
                            IceCandidates = transportInfo.IceCandidates,
                            DtlsParameters = transportInfo.DtlsParameters,
                            SctpParameters = transportInfo.SctpParameters,
                            IceServers = new RTCIceServer[] { },
                            //// AdditionalSettings = TODO: this goes to Handler.Run and as parametere to RTCPeerConnection???
                            //// ProprietaryConstraints = TODO: this goes to Handler.Run and as parametere to RTCPeerConnection???
                        });

                        _sendTransport.OnConnectAsync += SendTransport_OnConnectAsync;
                        _sendTransport.OnConnectionStateChangeAsync += SendTransport_OnConnectionStateChangeAsync;
                        _sendTransport.OnProduceAsync += SendTransport_OnProduceAsync;
                        _sendTransport.OnProduceDataAsync += SendTransport_OnProduceDataAsync;

                    }

                    // Create mediasoup Transport for receiving (unless we don't want to consume).
                    if (_consume)
                    {
                        var transportInfo = (TransportInfo)ParseResponse(MethodName.CreateWebRtcTransport,
                            await _mediaSoupServerApi.ApiAsync(MethodName.CreateWebRtcTransport,
                                new WebRtcTransportCreateRequest
                                {
                                    ForceTcp = forceTcp,
                                    Producing = false,
                                    Consuming = true,
                                    SctpCapabilities = _useDataChannel ? _mediaSoupDevice.SctpCapabilities : null
                                }));

                        _recvTransport = _mediaSoupDevice.CreateRecvTransport(new TransportOptions
                        {
                            Id = transportInfo.Id,
                            IceParameters = transportInfo.IceParameters,
                            IceCandidates = transportInfo.IceCandidates,
                            DtlsParameters = transportInfo.DtlsParameters,
                            SctpParameters = transportInfo.SctpParameters,
                            IceServers = new RTCIceServer[] { },
                            //// AdditionalSettings = TODO: this goes to Handler.Run and as parametere to RTCPeerConnection???
                            //// ProprietaryConstraints = TODO: this goes to Handler.Run and as parametere to RTCPeerConnection???
                        });

                        _recvTransport.OnConnectAsync += RecvTransport_OnConnectAsync;
                        _recvTransport.OnConnectionStateChangeAsync += RecvTransport_OnConnectionStateChangeAsync;
                    }

                    // Join now into the room.
                    // NOTE: Don't send our RTP capabilities if we don't want to consume.
                    var peers = (Peer[])ParseResponse(MethodName.Join,
                        await _mediaSoupServerApi.ApiAsync(MethodName.Join,
                            new JoinRequest
                            {
                                DisplayName = _displayName,
                                Device = _device,
                                RtpCapabilities = _consume ? _mediaSoupDevice.RtpCapabilities : null,
                                SctpCapabilities = _useDataChannel ? _mediaSoupDevice.SctpCapabilities : null
                            }));

                    foreach (var peer in peers)
                        OnNewPeer(peer);



                    if (_produce)
                    {
                        // Enable mic.
                        _micProducer = await _sendTransport.ProduceAsync(new ProducerOptions
                        {
                            Track = userContext.LocalStream.GetAudioTracks().First(),
                            Encodings = new RtpEncodingParameters[] { },
                            CodecOptions = new ProducerCodecOptions
                            {
                                OpusStereo = true,
                                OpusDtx = true
                            }
                        });

                        // Enable webcam.
                        RtpEncodingParameters[] encodings = null;
                        RtpCodecCapability codec = null;
                        ProducerCodecOptions codecOptions = new()
                        {
                            VideoGoogleStartBitrate = 1000
                        };

                        if (forceH264)
                        {
                            codec = _mediaSoupDevice.RtpCapabilities.Codecs
                                .FirstOrDefault(c => c.MimeType.ToLower() == "video/h264");
                            if (codec is null)
                                throw new Exception("Desired H264 codec+configuration is not supported");
                        }
                        else if (forceVP9)
                        {
                            codec = _mediaSoupDevice.RtpCapabilities.Codecs
                                .FirstOrDefault(c => c.MimeType.ToLower() == "video/vp9");
                            if (codec is null)
                                throw new Exception("Desired VP9 codec+configuration is not supported");
                        }

                        if (useSimulcast)
                        {
                            // If VP9 is the only available video codec then use SVC.
                            var firstVideoCodec = _mediaSoupDevice.RtpCapabilities.Codecs
                                .FirstOrDefault(c => c.Kind == MediaKind.Video);
                            if ((forceVP9 && codec is not null) ||
                                firstVideoCodec?.MimeType.ToLower() == "video/vp9")
                            {
                                encodings = new RtpEncodingParameters[]
                                {
                                    new()
                                    {
                                        ScalabilityMode = "S3T3_KEY"
                                    }
                                };
                            }
                            else
                            {
                                encodings = new RtpEncodingParameters[]
                                {
                                    new()
                                    {
                                        ScaleResolutionDownBy = 4,
                                        MaxBitrate = 500000
                                    },
                                    new()
                                    {
                                        ScaleResolutionDownBy = 2,
                                        MaxBitrate = 1000000
                                    },
                                    new()
                                    {
                                        ScaleResolutionDownBy = 1,
                                        MaxBitrate = 5000000
                                    }
                                };
                            }
                        }

                        _webcamProducer = await _sendTransport.ProduceAsync(new ProducerOptions
                        {
                            Track = userContext.LocalStream.GetVideoTracks().First(),
                            Encodings = encodings ?? new RtpEncodingParameters[] { },
                            CodecOptions = codecOptions,
                            Codec = codec

                        });

                        _ = ParseResponse(MethodName.PauseProducer,
                            await _mediaSoupServerApi.ApiAsync(MethodName.PauseProducer,
                                new PauseProducerRequest
                                {
                                    ProducerId = _webcamProducer.Id
                                })); ;


                        _ = ParseResponse(MethodName.ResumeProducer,
                            await _mediaSoupServerApi.ApiAsync(MethodName.ResumeProducer,
                                new ResumeProducerRequest
                                {
                                    ProducerId = _webcamProducer.Id
                                })); ;


                    }

                    //_connectionContext = new ConnectionContext
                    //{
                    //    UserContext = userContext,
                    //    Observer = observer
                    //};

                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return async () =>
                {
                    try
                    {
                        _mediaSoupServerApi.NotifyEventAsync -= OnNotifyAsync;
                        _mediaSoupServerApi.RequestEventAsync -= OnRequestAsync;
                        await _mediaSoupServerApi.DisconnectAsync(guid);
                        //await mediaServerProxy.StopAsync();
                    }
                    catch { };
                };

                async Task SendTransport_OnConnectAsync(object sender, DtlsParameters dtlsParameters)
                {
                    _logger.LogInformation($"-------> SendTransport_OnConnectAsync");
                    _ = ParseResponse(MethodName.ConnectWebRtcTransport,
                        await _mediaSoupServerApi.ApiAsync(MethodName.ConnectWebRtcTransport,
                            new WebRtcTransportConnectRequest
                            {
                                TransportId = _sendTransport.Id,
                                DtlsParameters = dtlsParameters
                            }));
                }


                async Task SendTransport_OnConnectionStateChangeAsync(object sender, ConnectionState connectionState)
                {
                    _logger.LogInformation($"-------> SendTransport_OnConnectionStateChangeAsync");
                    if (connectionState == ConnectionState.Connected)
                    {
                        if (_useDataChannel)
                        {
                            // Add chat data producer.
                            _chatDataProducer = await _sendTransport.ProduceDataAsync(new DataProducerOptions
                            {
                                Ordered = false,
                                MaxRetransmits = 1,
                                Label = "chat",
                                Protocol = "medium",
                                AppData = new Dictionary<string, object>() 
                                {
                                    { "info", "my-chat-DataProducer" }
                                }
                            });

                            // Add bot data producer.
                            _botDataProducer = await _sendTransport.ProduceDataAsync(new DataProducerOptions
                            {
                                Ordered = false,
                                MaxPacketLifeTime = 2000,
                                Label = "bot",
                                Protocol = "medium",
                                AppData = new Dictionary<string, object>()
                                {
                                    { "info", "my-bot-DataProducer" }
                                }
                            });

                        }
                    }
                }

                async Task<string> SendTransport_OnProduceAsync(object sender, ProduceEventParameters params_)
                {
                    _logger.LogInformation($"-------> SendTransport_OnProduceAsync");

                    var id = (string)ParseResponse(MethodName.Produce,
                        await _mediaSoupServerApi.ApiAsync(MethodName.Produce,
                            new ProduceRequest
                            {
                                TransportId = _sendTransport.Id,
                                Kind = params_.Kind,
                                RtpParameters = params_.RtpParameters,
                                AppData = params_.AppData ?? new Dictionary<string, object>()
                            }));
                    return id;
                }
                
                async Task<string> SendTransport_OnProduceDataAsync(object sender, ProduceDataEventParameters params_)
                {
                    _logger.LogInformation($"-------> SendTransport_OnProduceDataAsync");

                    var id = (string)ParseResponse(MethodName.ProduceData,
                        await _mediaSoupServerApi.ApiAsync(MethodName.ProduceData,
                            new ProduceDataRequest
                            {
                                TransportId = _sendTransport.Id,
                                SctpStreamParameters = params_.SctpStreamParameters,
                                Label = params_.Label,
                                Protocol = params_.Protocol,
                                AppData = params_.AppData ?? new Dictionary<string, object>()
                            }));
                    return id;

                }

                async Task RecvTransport_OnConnectAsync(object sender, DtlsParameters dtlsParameters)
                {
                    _logger.LogInformation($"-------> RecvTransport_OnConnectAsync");
                    _ = ParseResponse(MethodName.ConnectWebRtcTransport,
                        await _mediaSoupServerApi.ApiAsync(MethodName.ConnectWebRtcTransport,
                            new WebRtcTransportConnectRequest
                            {
                                TransportId = _recvTransport.Id,
                                DtlsParameters = dtlsParameters
                            }));
                }

                Task RecvTransport_OnConnectionStateChangeAsync(object sender, ConnectionState connectionState)
                {
                    _logger.LogInformation($"-------> RecvTransport_OnConnectionStateChangeAsync");
                    //if (connectionState == ConnectionState.Connected)
                    //{

                    //}

                    return Task.CompletedTask;
                }


            });


        }


        public Task OnNotifyAsync(string method, object data)
        {
            _logger.LogInformation($"=======> OnNotifyAsync: {method}");
            
            switch (method)
            {
                case MethodName.NewPeer:
                    {
                        var json = ((JsonElement)data).GetRawText();
                        var peer = JsonSerializer.Deserialize<Peer>(
                            json, JsonHelper.WebRtcJsonSerializerOptions);
                        OnNewPeer(peer);
                    }
                    break;

                default:
                    _logger.LogError($"-------> UNKNOWN Notification: {method}");
                    break;

            }

            return Task.CompletedTask;
        }

        public async Task OnRequestAsync(string method, object data,
            IMediaSoupServerNotify.Accept accept, IMediaSoupServerNotify.Reject reject)
        {
            _logger.LogInformation($"=======> OnRequestAsync: {method}");

            switch (method)
            {
                case MethodName.NewConsumer:
                    Consumer consumer = null;
                    if (!_consume)
                    {
                        reject(403, "I do not want to data consume");
                        return;
                    }

                    var consumerJson = ((JsonElement)data).GetRawText();
                    var consumerRequestData = JsonSerializer.Deserialize<ConsumerRequestParameters>(
                        consumerJson, JsonHelper.WebRtcJsonSerializerOptions);
                    
                    // Convert elements with Dictionary<string, object> to string or number or bool.
                    consumerRequestData.AppData.ToStringOrNumberOrBool();
                    foreach (var codec in consumerRequestData.RtpParameters.Codecs)
                        codec.Parameters.ToStringOrNumberOrBool();
                    foreach (var headerExtension in consumerRequestData.RtpParameters.HeaderExtensions)
                        headerExtension.Parameters.ToStringOrNumberOrBool();


                    var consumerAppData = consumerRequestData.AppData;
                    consumerAppData.Add(KeyName.PeerId, consumerRequestData.PeerId);  // trick

                    consumer = await _recvTransport.ConsumeAsync(new ConsumerOptions
                    {
                        Id = consumerRequestData.Id,
                        ProducerId = consumerRequestData.ProducerId,
                        Kind = consumerRequestData.Kind,
                        RtpParameters = consumerRequestData.RtpParameters,
                        AppData = consumerAppData
                    });

                    _consumers.Add(consumer.Id, consumer);
                    ////if (requestData.PeerId is not null)
                    {
                        var peer = _peers[consumerRequestData.PeerId];
                        peer.ConsumerIds.Add(consumer.Id);
                    }

                    consumer.OnClose += Consumer_OnClose;
                    consumer.OnTransportClosed += Consumer_OnTransportClosed;
                    consumer.OnTrackEnded += Consumer_OnTrackEnded;
                    consumer.OnGetStatsAsync += Consumer_OnGetStatsAsync;

                    accept();

                    // If audio-only mode is enabled, pause it.
                    ////if (consumer.Kind == MediaKind.Video && get 'audioOnly' from config)
                    ////consumer.Pause();


                    // Consumer is ready. Check if stream is ready (both audio and video).
                    // TODO: WE can have audio only calls!!!
                    ////if (requestData.PeerId is not null)
                    {
                        var consumerPeer = _peers[consumerRequestData.PeerId];
                        var consumers = consumerPeer.ConsumerIds
                            .Select(key => _consumers[key])
                            .ToList();

                        var audioConsumer =
                            consumers.FirstOrDefault(consumer => consumer.Kind == MediaKind.Audio);
                        var videoConsumer =
                            consumers.FirstOrDefault(consumer => consumer.Kind == MediaKind.Video);

                        // TODO: ASSUMED ONLY 1 video and 1 audio trak per peer.
                        if (audioConsumer is not null && videoConsumer is not null)
                        {
                            var mediaStream = _webRtc.Window(_jsRuntime).MediaStream();
                            mediaStream.AddTrack(audioConsumer.Track);
                            mediaStream.AddTrack(videoConsumer.Track);
                            _connectionContext.Observer.OnNext(new PeerResponse
                            {
                                Type = PeerResponseType.PeerJoined,
                                Id = Guid.NewGuid(),// TODO: HOW TO GET GUID FOR PEER ID??? requestData.PeerId,
                                Name = consumerRequestData.PeerId,//peer.Peer.DisplayName,
                                MediaStream = mediaStream,
                                DataChannel = /*isInitiator ? dataChannel :*/ null
                            });
                        }
                    }
                    break;

                    void Consumer_OnClose(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> Consumer_OnClose");
                        var peer = _peers[(string)consumer.AppData[KeyName.PeerId]];
                        peer.ConsumerIds.Remove(consumer.Id);
                        _consumers.Remove(consumer.Id);
                    }

                    void Consumer_OnTransportClosed(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> Consumer_OnTransportClose");
                        var peer = _peers[(string)consumer.AppData[KeyName.PeerId]];
                        peer.ConsumerIds.Remove(consumer.Id);
                        _consumers.Remove(consumer.Id);
                    }

                    void Consumer_OnTrackEnded(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> Consumer_OnTrackEnded");
                    }

                    Task<IRTCStatsReport> Consumer_OnGetStatsAsync(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> Consumer_GetStatsAsync");
                        return default;
                    }


                case MethodName.NewDataConsumer:
                    DataConsumer dataConsumer = null;
                    if (!_consume)
                    {
                        reject(403, "I do not want to data consume");
                        return;
                    }
                    if (!_useDataChannel)
                    {
                        reject(403, "I do not want DataChannels");
                        return;
                    }

                    var dataConsumerJson = ((JsonElement)data).GetRawText();
                    //_logger.LogInformation($"NewDataConsumer.JSON: {json}");
                    var dataConsumerRequestData = JsonSerializer.Deserialize<DataConsumerRequestParameters>(
                        dataConsumerJson, JsonHelper.WebRtcJsonSerializerOptions);

                    // Convert elements with Dictionary<string, object> to string or number or bool.
                    dataConsumerRequestData.AppData.ToStringOrNumberOrBool();

                    var appData = dataConsumerRequestData.AppData;
                    appData.Add(KeyName.PeerId, dataConsumerRequestData.PeerId);  // trick

                    // Invoke accept here, ConsumerDataAsync call assumes DataConsumer is already created.
                    ////accept();


                    dataConsumer = await _recvTransport.ConsumeDataAsync(new DataConsumerOptions
                    {
                        Id = dataConsumerRequestData.Id,
                        DataProducerId = dataConsumerRequestData.DataProducerId,
                        SctpStreamParameters = dataConsumerRequestData.SctpStreamParameters,
                        Label = dataConsumerRequestData.Label,
                        Protocol = dataConsumerRequestData.Protocol,
                        AppData = appData
                    });

                    _dataConsumers.Add(dataConsumer.Id, dataConsumer);
                    if (dataConsumerRequestData.PeerId is not null)
                    {
                        var dataConsumerPeer = _peers[dataConsumerRequestData.PeerId];
                        dataConsumerPeer.DataConsumerIds.Add(dataConsumer.Id);
                    }

                    dataConsumer.OnOpen += DataConsumer_OnOpen;
                    dataConsumer.OnClose += DataConsumer_OnClose;
                    dataConsumer.OnTransportClosed += DataConsumer_OnTransportClosed;
                    dataConsumer.OnError += DataConsumer_OnError;
                    dataConsumer.OnMessage += DataConsumer_OnMessage;

                    accept();
                    break;

                    //// TODO: HOW TO DEREGISTER EVENTS???
                    void DataConsumer_OnOpen(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> DataConsumer_OnOpen");

                    }
                    void DataConsumer_OnClose(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> DataConsumer_OnClose");
                        var peer = _peers[(string)dataConsumer.AppData[KeyName.PeerId]];
                        peer.DataConsumerIds.Remove(dataConsumer.Id);
                        _dataConsumers.Remove(dataConsumer.Id);
                    }

                    void DataConsumer_OnTransportClosed(object sender, EventArgs e)
                    {
                        _logger.LogInformation($"-------> DataConsumer_OnTransportClosed");
                        var peer = _peers[(string)dataConsumer.AppData[KeyName.PeerId]];
                        peer.DataConsumerIds.Remove(dataConsumer.Id);
                        _dataConsumers.Remove(dataConsumer.Id);

                    }

                    void DataConsumer_OnError(object sender, IErrorEvent e)
                    {
                        _logger.LogInformation($"-------> DataConsumer_OnError");
                    }

                    void DataConsumer_OnMessage(object sender, IMessageEvent e)
                    {
                        _logger.LogInformation($"-------> DataConsumer_OnMessage");
                        throw new NotImplementedException();
                    }

                default:
                    _logger.LogError($"-------> UNKNOWN Request: {method}");
                    break;
            }


        }


        public Task<IRTCStatsReport> GetStats(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task ReplaceOutgoingTrackAsync(IMediaStreamTrack track, IMediaStreamTrack newTrack)
        {
            throw new NotImplementedException();
        }

        object ParseResponse(string method, Result<object> result)
        {
            _logger.LogInformation($"######## CallAsync Response: {method}");

            if (!result.IsOk)
                throw new Exception(result.ErrorMessage);

            var data = result.Value;
            var json = ((JsonElement)data).GetRawText();

   ////_logger.LogInformation($"JSON: {json}");

            switch (method)
            {
                case MethodName.GetRouterRtpCapabilities:
                    var routerRtpCapabilities = JsonSerializer.Deserialize<RtpCapabilities>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    
                    // Need to convert object (Parameters.Value) to either string or int.
                    foreach (var codec in routerRtpCapabilities.Codecs)
                    {
                        codec.Parameters.ToStringOrNumber();
                    }

                     return routerRtpCapabilities;

                case MethodName.CreateWebRtcTransport:
                    var transportInfo = JsonSerializer.Deserialize<TransportInfo>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    return transportInfo;

                case MethodName.Join:
                    var joinResponse = JsonSerializer.Deserialize<JoinResponse>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    var peers = joinResponse.Peers;
                    return peers;

                case MethodName.ConnectWebRtcTransport:
                    return null;

                case MethodName.Produce:
                    var produceResponse = JsonSerializer.Deserialize<ProduceResponse>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    return produceResponse.Id;

                case MethodName.PauseProducer:
                    var pauseProducerResponse = JsonSerializer.Deserialize<PauseProducerRequest>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    return pauseProducerResponse;

                case MethodName.ResumeProducer:
                    var resumeProducerResponse = JsonSerializer.Deserialize<ResumeProducerRequest>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    return resumeProducerResponse;

                case MethodName.ProduceData:
                    var produceDataResponse = JsonSerializer.Deserialize<ProduceDataResponse>(
                        json, JsonHelper.WebRtcJsonSerializerOptions);
                    return produceDataResponse.Id;
            }

            return null;

        }


        void OnNewPeer(Peer peer)
        {
            _peers.Add(peer.Id, new PeerParameters 
            { 
                Peer = peer,
                ConsumerIds = new(),
                DataConsumerIds =new(),
            });
        }

        MediaSoup.Proxy.Models.Device GetDevice()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
                return new MediaSoup.Proxy.Models.Device
                {
                    Flag = "Android",
                    Name = DeviceInfo.Name,
                    Version = DeviceInfo.Version.ToString()
                };
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
                return new MediaSoup.Proxy.Models.Device
                {
                    Flag = "iOS",
                    Name = DeviceInfo.Name,
                    Version = DeviceInfo.Version.ToString()
                };
            else
                return new MediaSoup.Proxy.Models.Device
                {
                    Flag = "Blazor",
                    Name = "Browser",
                    Version = "1.0"
                };
        }
    }
}
