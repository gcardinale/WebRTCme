﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using WebRTCme.SignallingServerProxy;
using WebRTCme.Middleware;
using WebRTCme.Middleware.Services;
using Microsoft.JSInterop;

namespace WebRTCme.Middleware
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddMiddleware(this IServiceCollection services)
        {
            services.AddSignallingServerProxy();
            services.AddSingleton<ILocalMediaStream, LocalMediaStream>();
            services.AddSingleton<IWebRtcConnection, WebRtcConnection>();
            services.AddSingleton<ISignallingServer, SignallingServer>();
            services.AddSingleton<IMediaStreamManager, MediaStreamManager>();
            services.AddSingleton<IDataManager, DataManager>();
            services.AddSingleton<IMediaRecorderManager, MediaRecorderManager>();
            services.AddSingleton<InitializingViewModel>();
            services.AddSingleton<ConnectionParametersViewModel>();
            services.AddSingleton<CallViewModel>();
            services.AddSingleton<ChatViewModel>();

            return services;
        }
    }
}
