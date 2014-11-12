using Microsoft.AspNet.Http;
using System;

namespace RequestResponsePatterns
{
    public static class HttpResponseRestartExtensions
    {
        public static bool CanRestart(this HttpResponse response)
        {
            return response.HeadersSent == false && response.Body.CanSeek;
        }

        public static void Restart(this HttpResponse response)
        {
            response.Body.SetLength(0);
            response.Headers.Clear();
        }

        public static void EnsureWTFBIGY(this HttpResponse response)
        {
            var wtfbigy = response.HttpContext.GetFeature<IWTFBIGYFeature>();
            if (wtfbigy != null)
            {
                wtfbigy.EnsureWTFBIGY();
            }
        }
    }
}
