﻿using System;
using Xamarin.Forms;
using DemoApp.Views;
using WebRTCme.Middleware.Xamarin;
using Microsoft.Extensions.Configuration;
using Xamarinme;
using System.Reflection;
using WebRTCme.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WebRTCme.DemoApp.Xamarin.Services;

namespace DemoApp
{
    public partial class App : Application
    {
        public static IHost Host { get; private set; }

        public App()
        {
            var hostBuilder = XamarinHostBuilder.CreateDefault(new EmbeddedResourceConfigurationOptions
            {
                Assembly = Assembly.GetExecutingAssembly(),
                Prefix = "WebRTCme.DemoApp.Xamarin"
            });


            _ = CrossWebRtcMiddlewareXamarin.Current;
            hostBuilder.Services.AddSingleton<INavigationService, NavigationService>();
            hostBuilder.Services.AddMiddleware();
            Host = hostBuilder.Build();

            InitializeComponent();

            MainPage = new AppShell();
        }

        protected override async void OnStart()
        {
            //var mediaStreamService = Host.Services.GetService<IMediaStreamService>();
            //await mediaStreamService.Initialization;
            //var signallingServerService = Host.Services.GetService<ISignallingServerService>();
            //await signallingServerService.Initialization;
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }

        protected override /*async*/ void CleanUp()
        {
            ////            WebRtcMiddleware.Cleanup();
            ///
            //await SignallingServerService.DisposeAsync();
            //WebRtcMiddleware.Dispose();

            base.CleanUp();
        }
    }
}
