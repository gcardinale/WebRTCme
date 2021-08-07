﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Utilme;
using WebRTCme.Connection.MediaSoup;
using WebRTCme.Connection.MediaSoup.Proxy.Client;
using WebRTCme.Connection.MediaSoup.Proxy.Models;
using Xamarin.Essentials;

namespace WebRTCme.Connection.Services
{
    class MediaSoupConnection : IConnection, IMediaSoupServerNotify
    {
        readonly IConfiguration _configuration;
        readonly IMediaSoupServerApi _mediaSoupServerApi;
        readonly ILogger<MediaSoupConnection> _logger;
        readonly IWebRtc _webRtc;
        readonly IJSRuntime _jsRuntime;
        readonly MediaSoup.Proxy.Models.Device _device;

        Transport _sendTransport;
        Transport _recvTransport;
        string _displayName;

        bool _produce;
        bool _consume;
        bool _useDataChannel;


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

                _displayName = userContext.Name;

                try
                {
                    _mediaSoupServerApi.NotifyEventAsync += OnNotifyAsync;
                    _mediaSoupServerApi.RequestEventAsync += OnRequestAsync;

                    await _mediaSoupServerApi.ConnectAsync(guid, userContext.Name, userContext.Room);

                    var mediaSoupDevice = new MediaSoup.Proxy.Client.Device();

                    var routerRtpCapabilities = (RtpCapabilities)ParseResponse(MethodName.GetRouterRtpCapabilities,
                        await _mediaSoupServerApi.CallAsync(MethodName.GetRouterRtpCapabilities));
                    await mediaSoupDevice.LoadAsync(routerRtpCapabilities);


                    // Create mediasoup Transport for sending (unless we don't want to produce).
                    if (_produce)
                    {
                        var transportInfo = (TransportInfo)ParseResponse(MethodName.CreateWebRtcTransport,
                            await _mediaSoupServerApi.CallAsync(MethodName.CreateWebRtcTransport,
                                new WebRtcTransportCreateParameters
                                {
                                    ForceTcp = forceTcp,
                                    Producing = true,
                                    Consuming = false,
                                    SctpCapabilities = _useDataChannel ? mediaSoupDevice.SctpCapabilities : null
                                }));

                        _sendTransport = mediaSoupDevice.CreateSendTransport(new TransportOptions 
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
                        _sendTransport.OnProduceAsync += SendTransport_OnProduceAsync;
                        _sendTransport.OnProduceDataAsync += SendTransport_OnProduceDataAsync;

                    }

                    // Create mediasoup Transport for receiving (unless we don't want to consume).
                    if (_consume)
                    {
                        var transportInfo = (TransportInfo)ParseResponse(MethodName.CreateWebRtcTransport,
                            await _mediaSoupServerApi.CallAsync(MethodName.CreateWebRtcTransport,
                                new WebRtcTransportCreateParameters
                                {
                                    ForceTcp = forceTcp,
                                    Producing = false,
                                    Consuming = true,
                                    SctpCapabilities = _useDataChannel ? mediaSoupDevice.SctpCapabilities : null
                                }));

                        _recvTransport = mediaSoupDevice.CreateRecvTransport(new TransportOptions
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
                    }


                    // Join now into the room.
                    // NOTE: Don't send our RTP capabilities if we don't want to consume.
                    var peers = (Peer[])ParseResponse(MethodName.Join,
                        await _mediaSoupServerApi.CallAsync(MethodName.Join,
                            new JoinParameters
                            {
                                DisplayName = _displayName,
                                Device = _device,
                                RtpCapabilities = _consume ? mediaSoupDevice.RtpCapabilities : null,
                                SctpCapabilities = _useDataChannel ? mediaSoupDevice.SctpCapabilities : null
                            }));



                    //connectionContext = new ConnectionContext
                    //{
                    //    ConnectionRequestParameters = request,
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

                Task SendTransport_OnConnectAsync(object sender, DtlsParameters dtlsParameters)
                {
                    throw new NotImplementedException();
                }

                Task<string> SendTransport_OnProduceAsync(object sender, ProduceEventParameters params_)
                {
                    throw new NotImplementedException();
                }
                
                Task<string> SendTransport_OnProduceDataAsync(object sender, ProduceDataEventParameters params_)
                {
                    throw new NotImplementedException();
                }

                Task RecvTransport_OnConnectAsync(object sender, DtlsParameters e)
                {
                    throw new NotImplementedException();
                }

            });


        }


        public Task OnNotifyAsync(string method, object data)
        {
            throw new NotImplementedException();
        }

        public async Task OnRequestAsync(string method, object data,
            IMediaSoupServerNotify.Accept accept, IMediaSoupServerNotify.Reject reject)
        {
            switch (method)
            {
                case MethodName.NewConsumer:
                    {

                    }
                    break;

                case MethodName.NewDataConsumer:
                    {
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

                        var json = ((JsonElement)data).GetRawText();
   //_logger.LogInformation($"NewDataConsumer.JSON: {json}");
                        var requestData = JsonSerializer.Deserialize<DataConsumerRequestParameters>(
                            json, JsonHelper.CamelCaseAndIgnoreNullJsonSerializerOptions);

                        var dataConsumer = await _recvTransport.ConsumerDataAsync(new DataConsumerOptions 
                        {
                            Id = requestData.Id,
                            DataProducerId = requestData.DataProducerId,
                            SctpStreamParameters = requestData.SctpStreamParameters,
                            Label = requestData.Label,
                            Protocol = requestData.Protocol,
                            AppData = requestData.AppData
                        });



                        accept();
                    }
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
            if (!result.IsOk)
                throw new Exception(result.ErrorMessage);

            var data = result.Value;
            var json = ((JsonElement)data).GetRawText();

   ////_logger.LogInformation($"JSON: {json}");

            switch (method)
            {
                case MethodName.GetRouterRtpCapabilities:
                    var routerRtpCapabilities = JsonSerializer.Deserialize<RtpCapabilities>(
                        json, JsonHelper.CamelCaseAndIgnoreNullJsonSerializerOptions);
                    
                    // Need to convert object (Parameters.Value) to either string or int.
                    foreach (var codec in routerRtpCapabilities.Codecs)
                    {
                        foreach (var item in codec.Parameters)
                        {
                            var jElement = (JsonElement)item.Value;
                            if (jElement.ValueKind == JsonValueKind.String)
                                codec.Parameters[item.Key] = jElement.GetString();
                            else if (jElement.ValueKind == JsonValueKind.Number)
                                codec.Parameters[item.Key] = jElement.GetInt32();
                        }
                    }

                    return routerRtpCapabilities;

                case MethodName.CreateWebRtcTransport:
                    var transportInfo = JsonSerializer.Deserialize<TransportInfo>(
                        json, JsonHelper.CamelCaseAndIgnoreNullJsonSerializerOptions);
                    return transportInfo;

                case MethodName.Join:
                    var joinResponse = JsonSerializer.Deserialize<JoinResponse>(
                        json, JsonHelper.CamelCaseAndIgnoreNullJsonSerializerOptions);
                    var peers = joinResponse.Peers;
                    return peers;
            }

            return null;

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
