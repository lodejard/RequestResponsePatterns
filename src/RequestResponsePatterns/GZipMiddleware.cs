using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading;

namespace RequestResponsePatterns
{
    public class GZipMiddleware
    {
        private readonly RequestDelegate _next;

        public GZipMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            using (var handler = new GZipHandler(context))
            {
                await _next(context);
                await handler.FinishAsync();
            }
        }
    }

    public class GZipHandler : IDisposable, IDelegatingResponseHandler, IWTFBIGYFeature
    {
        private readonly HttpContext _context;
        private readonly Stream _priorResponseBody;
        private readonly IWTFBIGYFeature _priorWTFBIGYFeature;
        private GZipStream _compression;
        private State _state = State.Uninitialized;
        enum State
        {
            Uninitialized,
            Working,
            NotWorking
        }

        public GZipHandler(HttpContext context)
        {
            _context = context;
            _priorResponseBody = _context.Response.Body;
            _context.Response.Body = new DelegatingResponseBody(_priorResponseBody, this);
            _priorWTFBIGYFeature = _context.GetFeature<IWTFBIGYFeature>();
            _context.SetFeature<IWTFBIGYFeature>(this);
        }

        public bool FullBuffer { get { return false; } }

        public void Dispose()
        {
            _context.Response.Body = _priorResponseBody;
            _context.SetFeature(_priorWTFBIGYFeature);
        }

        public void EnsureWTFBIGY()
        {
            if (_state == State.Uninitialized)
            {
                _state = State.NotWorking;
            }
            if (_priorWTFBIGYFeature != null)
            {
                _priorWTFBIGYFeature.EnsureWTFBIGY();
            }
        }

        bool CheckWorking()
        {
            if (_state == State.Uninitialized)
            {
                var teHeaders = _context.Request.Headers.GetCommaSeparatedValues("TE");
                var acceptEncodingHeaders = _context.Request.Headers.GetCommaSeparatedValues("Accept-Encoding");
                if (teHeaders != null && teHeaders.Contains("gzip"))
                {
                    _state = State.Working;
                    _context.Response.Headers.AppendCommaSeparatedValues("Transfer-Encoding", "gzip");
                    _compression = new GZipStream(
                        _priorResponseBody,
                        CompressionLevel.Fastest,
                        leaveOpen: true);
                }
                else if (acceptEncodingHeaders != null && acceptEncodingHeaders.Contains("gzip"))
                {
                    // TODO: alter etag
                    _state = State.Working;
                    _context.Response.Headers.AppendCommaSeparatedValues("Content-Encoding", "gzip");
                    _compression = new GZipStream(
                        _priorResponseBody,
                        CompressionLevel.Fastest,
                        leaveOpen: true);
                }
                else
                {
                    _state = State.NotWorking;
                }
            }
            return _state == State.Working;
        }

        public void Restart()
        {
            _state = State.Uninitialized;
            _compression = null;
        }

        public void Write(ArraySegment<byte> data)
        {
            if (CheckWorking())
            {
                _compression.Write(data.Array, data.Offset, data.Count);
            }
            else
            {
                _priorResponseBody.Write(data.Array, data.Offset, data.Count);
            }
        }

        public Task WriteAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            if (CheckWorking())
            {
                return _compression.WriteAsync(data.Array, data.Offset, data.Count, cancellationToken);
            }
            else
            {
                return _priorResponseBody.WriteAsync(data.Array, data.Offset, data.Count, cancellationToken);
            }
        }

        public void Flush()
        {
            if (CheckWorking())
            {
                _compression.Flush();
                _priorResponseBody.Flush();
            }
            else
            {
                _priorResponseBody.Flush();
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (CheckWorking())
            {
                await _compression.FlushAsync(cancellationToken);
                await _priorResponseBody.FlushAsync(cancellationToken);
            }
            else
            {
                await _priorResponseBody.FlushAsync(cancellationToken);
            }
        }

        public async Task FinishAsync()
        {
            if (_state == State.Working)
            {
                _compression.Close();
            }
        }
    }
}
