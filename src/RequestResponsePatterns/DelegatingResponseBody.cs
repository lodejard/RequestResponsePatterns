using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RequestResponsePatterns
{
    public interface IDelegatingResponseHandler
    {
        bool FullBuffer { get; }
        void Write(ArraySegment<byte> data);
        Task WriteAsync(ArraySegment<byte> data, CancellationToken cancellationToken);
        void Flush();
        Task FlushAsync(CancellationToken cancellationToken);
        void Restart();
    }

    public class DelegatingResponseBody : Stream
    {
        private readonly Stream _inner;
        private readonly IDelegatingResponseHandler _handler;
        private readonly bool _fullBuffer;
        private long _bytesWritten;

        public DelegatingResponseBody(Stream inner, IDelegatingResponseHandler handler)
        {
            _inner = inner;
            _handler = handler;
            _fullBuffer = handler.FullBuffer;
        }

        void Restart()
        {
            if (_inner.CanSeek)
            {
                _inner.SetLength(0);
            }
            _handler.Restart();
            _bytesWritten = 0;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override int ReadByte()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotImplementedException();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }


        public override bool CanWrite
        {
            get { return true; }
        }

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _bytesWritten += count;
            _handler.Write(new ArraySegment<byte>(buffer, offset, count));
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _bytesWritten += count;
            return _handler.WriteAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return base.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            base.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            _handler.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _handler.FlushAsync(cancellationToken);
        }

        public override bool CanTimeout
        {
            get { return _inner.CanTimeout; }
        }

        public override int ReadTimeout
        {
            get { return _inner.ReadTimeout; }
            set { _inner.ReadTimeout = value; }
        }

        public override int WriteTimeout
        {
            get { return _inner.WriteTimeout; }
            set { _inner.WriteTimeout = value; }

        }

        public override bool CanSeek
        {
            get { return _fullBuffer || _inner.CanSeek; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position = Position + offset;
                break;
            case SeekOrigin.End:
                Position = Length + offset;
                break;
            }
            return Position;
        }

        public override long Length
        {
            get { return _bytesWritten; }
        }

        public override long Position
        {
            get
            {
                return _bytesWritten;
            }

            set
            {
                if (value != 0)
                {
                    throw new ArgumentOutOfRangeException();
                }
                Restart();
            }
        }

        public override void SetLength(long value)
        {
            if (value != 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            Restart();
        }

        protected override void Dispose(bool disposing)
        {
            // NOOP
        }

        public override void Close()
        {
            // NOOP
        }
    }
}
