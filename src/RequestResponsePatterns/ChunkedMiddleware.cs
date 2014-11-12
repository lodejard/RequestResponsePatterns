using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RequestResponsePatterns
{
    public class ChunkedMiddleware
    {
        private readonly RequestDelegate _next;

        public ChunkedMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            using (var handler = new ChunkedHandler(context))
            {
                await _next(context);
                await handler.FinishAsync();
            }
        }
    }

    public class ChunkedHandler : IDisposable, IDelegatingResponseHandler
    {
        private readonly HttpContext _context;
        private readonly Stream _priorResponseBody;
        private State _state = State.Uninitialized;

        enum State
        {
            Uninitialized,
            Working,
            NotWorking
        }

        public ChunkedHandler(HttpContext context)
        {
            _context = context;
            _priorResponseBody = _context.Response.Body;
            _context.Response.Body = new DelegatingResponseBody(_priorResponseBody, this);
        }

        public bool FullBuffer { get { return false; } }

        public void Dispose()
        {
            _context.Response.Body = _priorResponseBody;
        }

        bool CheckWorking()
        {
            if (_state == State.Uninitialized)
            {
                if (_context.Response.Headers.ContainsKey("Content-Length"))
                {
                    _state = State.NotWorking;
                }
                else
                {
                    // TODO detect Content-Length, Transfer-Encoding:chunked, or Connection:close already set
                    _context.Response.Headers.AppendCommaSeparatedValues("Transfer-Encoding", "chunked");
                    _state = State.Working;
                }
            }
            return _state == State.Working;
        }


        public void Restart()
        {
            _state = State.Uninitialized;
        }

        public void Write(ArraySegment<byte> data)
        {
            if (CheckWorking())
            {
                if (data.Count != 0)
                {
                    byte[] initial = Encoding.ASCII.GetBytes(data.Count.ToString("x") + "\r\n");
                    byte[] final = new byte[] { (byte)'\r', (byte)'\n' };
                    _priorResponseBody.Write(initial, 0, initial.Length);
                    _priorResponseBody.Write(data.Array, data.Offset, data.Count);
                    _priorResponseBody.Write(final, 0, 2);
                }
            }
            else
            {
                _priorResponseBody.Write(data.Array, data.Offset, data.Count);
            }
        }

        public async Task WriteAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            if (CheckWorking())
            {
                if (data.Count != 0)
                {
                    byte[] initial = Encoding.ASCII.GetBytes(data.Count.ToString("x") + "\r\n");
                    byte[] final = new byte[] { (byte)'\r', (byte)'\n' };
                    await _priorResponseBody.WriteAsync(initial, 0, initial.Length, cancellationToken);
                    await _priorResponseBody.WriteAsync(data.Array, data.Offset, data.Count, cancellationToken);
                    await _priorResponseBody.WriteAsync(final, 0, 2, cancellationToken);
                }
            }
            else
            {
                await _priorResponseBody.WriteAsync(data.Array, data.Offset, data.Count, cancellationToken);
            }
        }

        public void Flush()
        {
            CheckWorking();
            _priorResponseBody.Flush();
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            CheckWorking();
            return _priorResponseBody.FlushAsync(cancellationToken);
        }

        public Task FinishAsync()
        {
            if (_state == State.Working)
            {
                byte[] final = new byte[] { (byte)'0', (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
                return _priorResponseBody.WriteAsync(final, 0, 5);
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }
}

