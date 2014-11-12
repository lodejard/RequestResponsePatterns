using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace RequestResponsePatterns
{
    public class ResponseBufferMiddleware
    {
        private readonly RequestDelegate _next;

        public ResponseBufferMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            return context.Response.Body.CanSeek ? _next(context) : InvokeHandler(context);
        }

        public async Task InvokeHandler(HttpContext context)
        {
            using (var handler = new ResponseBufferHandler(context))
            {
                await _next(context);
                await handler.FinishAsync();
            }
        }
    }

    public class ResponseBufferHandler : IDisposable, IDelegatingResponseHandler, IWTFBIGYFeature
    {
        private readonly HttpContext _context;
        private readonly Stream _priorResponseBody;
        private readonly IWTFBIGYFeature _priorWTFBIGYFeature;
        private Stream _buffer;
        private State _state = State.Uninitialized;
        enum State
        {
            Uninitialized,
            Working,
            NotWorking,
            Flushed
        }

        public ResponseBufferHandler(HttpContext context)
        {
            _context = context;
            _priorResponseBody = _context.Response.Body;
            _context.Response.Body = new DelegatingResponseBody(_priorResponseBody, this);
            _priorWTFBIGYFeature = _context.GetFeature<IWTFBIGYFeature>();
            _context.SetFeature<IWTFBIGYFeature>(this);
        }

        public bool FullBuffer { get { return true; } }

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
                if (_context.Response.Headers.ContainsKey("Transfer-Encoding"))
                {
                    _state = State.NotWorking;
                }
                else
                {
                    _buffer = new MemoryStream();
                    _state = State.Working;
                }
            }
            return _state == State.Working;
        }

        public void Restart()
        {
            if (_state != State.Flushed)
            {
                _state = State.Uninitialized;
                _buffer = null;
            }
        }

        public void Write(ArraySegment<byte> data)
        {
            if (CheckWorking())
            {
                _buffer.Write(data.Array, data.Offset, data.Count);
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
                _buffer.Write(data.Array, data.Offset, data.Count);
                return Task.FromResult(0);
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
                _state = State.Flushed;
                _buffer.Seek(0, SeekOrigin.Begin);
                _buffer.CopyTo(_priorResponseBody);
                _buffer.SetLength(0);
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
                _state = State.Flushed;
                _buffer.Seek(0, SeekOrigin.Begin);
                await _buffer.CopyToAsync(_priorResponseBody, 8192, cancellationToken);
                _buffer.SetLength(0);
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
                _context.Response.ContentLength = _buffer.Length;
                _buffer.Seek(0, SeekOrigin.Begin);
                await _buffer.CopyToAsync(_priorResponseBody);
                _buffer.SetLength(0);
            }
        }
    }
}