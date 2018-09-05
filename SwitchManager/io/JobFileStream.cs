using System;
using System.IO;
using System.Threading.Tasks;

namespace SwitchManager.io
{
    internal class JobFileStream : Stream, IDisposable
    {
        private FileStream str;
        private FileWriteJob job;
        private readonly int chunkSize;

        public JobFileStream(FileStream str, string jobName, long expectedSize, int startingSize, int chunkSize = 8192)
        {
            this.str = str;
            this.job = new FileWriteJob(str, jobName, expectedSize, startingSize);
            this.chunkSize = chunkSize;
            job.Start();
        }

        public JobFileStream(string path, string jobName, long expectedSize, int startingSize, int bufferSize = 8192) : this(File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite), jobName, expectedSize, startingSize, bufferSize)
        {
        }

        public override bool CanRead => str?.CanRead ?? false;

        public override bool CanSeek => str?.CanSeek ?? false;

        public override bool CanWrite => str?.CanWrite ?? false;

        public override long Length => str?.Length ?? 0;

        public override long Position { get => str?.Position ?? 0; set { if (this.str != null) str.Position = value; } }

        public new void Dispose()
        {
            if (str != null) str.Dispose();
            Finish();
        }

        public void Finish()
        {
            job.Finish();
        }

        public override void Flush()
        {
            if (str != null) str.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (str == null) return 0;
            if (count > buffer.Length) count = buffer.Length;
            if (count < 0) count = 0;

            int index = 0;
            while (true)
            {
                // Read a chunk from the stream
                int n = str.Read(buffer, index, Math.Min(count, chunkSize));

                if (n == 0 || job.IsCancelled)
                {
                    // There is nothing else to read.
                    break;
                }

                // Report progress.
                job.UpdateProgress(n);

                index += n;
                count -= n;
            }

            return index;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (str != null)
                return str.Seek(offset, origin);
            else
                return 0;
        }

        public override void SetLength(long value)
        {
            if (str != null) str.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (str == null) return;
            if (count > buffer.Length) count = buffer.Length;
            if (count < 0) count = 0;

            int index = 0;
            while (true)
            {
                int howMany = Math.Min(count, chunkSize);

                // Write a chunk to the file
                str.Write(buffer, index, howMany);

                // Report progress.
                job.UpdateProgress(howMany);

                index += howMany;
                count -= howMany;

                if (count <= 0 || job.IsCancelled)
                {
                    // There is nothing else to read.
                    break;
                }
            }
        }

        public long CopyFrom(Stream source)
        {
            if (str == null) return 0;
            if (source == null) return 0;

            byte[] buffer = new byte[chunkSize];
            long written = 0;
            while (true)
            {
                // Read from the web.
                int n = source.Read(buffer, 0, buffer.Length);

                if (n == 0 || job.IsCancelled)
                {
                    // There is nothing else to read.
                    break;
                }

                // Report progress.
                job.UpdateProgress(n);

                // Write to file.
                str.Write(buffer, 0, n);
                written += n;
            }

            return written;
        }

        public async Task<long> CopyFromAsync(Stream source)
        {
            if (str == null) return 0;
            if (source == null) return 0;

            byte[] buffer = new byte[chunkSize];
            long written = 0;
            while (true)
            {
                // Read from the web.
                int n = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                if (n == 0 || job.IsCancelled)
                {
                    // There is nothing else to read.
                    break;
                }

                // Report progress.
                job.UpdateProgress(n);

                // Write to file.
                await str.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                written += n;
            }

            return written;
        }

        public new long CopyTo(Stream dest)
        {
            if (str == null) return 0;
            if (dest == null) return 0;

            byte[] buffer = new byte[chunkSize];
            long written = 0;
            while (true)
            {
                // Read from the web.
                int n = str.Read(buffer, 0, buffer.Length);

                if (n == 0 || job.IsCancelled)
                {
                    // There is nothing else to read.
                    break;
                }

                // Report progress.
                job.UpdateProgress(n);

                // Write to file.
                dest.Write(buffer, 0, n);
                written += n;
            }

            return written;
        }

        public new async Task<long> CopyToAsync(Stream dest)
        {
            return await CopyToAsync(dest, -1).ConfigureAwait(false);
        }

        public async Task<long> CopyToAsync(Stream dest, long howManyBytes)
        {
            if (str == null) return 0;
            if (dest == null) return 0;

            byte[] buffer = new byte[chunkSize];
            long written = 0;
            long bytesLeft = howManyBytes;
            while (true)
            {
                // Cancelled job
                if (job.IsCancelled) break;

                // Read from the source.
                int b = howManyBytes > 0 && bytesLeft < chunkSize ? (int)bytesLeft : chunkSize;
                int n = await str.ReadAsync(buffer, 0, b).ConfigureAwait(false);

                // There is nothing else to read.
                if (n == 0) break;

                // Report progress.
                job.UpdateProgress(n);

                // Write to file.
                await dest.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                written += n;
                bytesLeft -= n;

                // Finished writing all the requested bytes
                if (howManyBytes > 0 && written >= howManyBytes) break;
            }

            return written;
        }
    }
}