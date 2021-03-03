﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebRTCme;
using WebRtcMeMiddleware;
using WebRtcMeMiddleware.Services;

namespace WebRTCme.Middleware
{
    public class WebRtcMiddleware : IWebRtcMiddleware
    {
        public static IWebRtc WebRtc { get; private set; }

        public WebRtcMiddleware(IWebRtc webRtc)
        {
            WebRtc = webRtc;
        }


        #region IDisposable
        private bool _isDisposed = false;


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed.
                    WebRtc.Cleanup();
                }

                _isDisposed = true;
            }
        }


        #endregion
    }
}
