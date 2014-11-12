using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.Threading.Tasks;

namespace RequestResponsePatterns
{
    public class WTFBIGYAcceptEncodingMiddleware
    {
        private readonly RequestDelegate _next;

        public WTFBIGYAcceptEncodingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            using (new Handler(context))
            {
                await _next(context);
            }
        }

        class Handler : IDisposable, IWTFBIGYFeature
        {
            private HttpContext _context;
            private readonly IWTFBIGYFeature _priorWTFBIGYFeature;

            public Handler(HttpContext context)
            {
                _context = context;
                _priorWTFBIGYFeature = _context.GetFeature<IWTFBIGYFeature>();
                _context.SetFeature<IWTFBIGYFeature>(this);
            }

            public void Dispose()
            {
                _context.SetFeature(_priorWTFBIGYFeature);
            }

            public void EnsureWTFBIGY()
            {
                _context.Request.Headers.Remove("Accept-Encoding");
                if (_priorWTFBIGYFeature != null)
                {
                    _priorWTFBIGYFeature.EnsureWTFBIGY();
                }
            }
        }
    }
}