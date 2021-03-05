using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HermaFx;

namespace SmppSimCatcher.Facilities
{
	public class FileSourceReader : IDisposable
	{
		#region Fields & Constants
		private static readonly global::Common.Logging.ILog _Log = global::Common.Logging.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static readonly Encoding DEFAULT_ENCODING = Encoding.UTF8;
		private static readonly IDictionary<byte[], Encoding> ENCODINGS_WITH_BOM =
			new[] { Encoding.UTF8, Encoding.Unicode, new UnicodeEncoding(true, true), Encoding.UTF32, new UTF32Encoding(true, true) }.ToDictionary(x => x.GetPreamble(), x => x);
		private static readonly IEnumerable<byte[]> BOM_PREAMBLES = ENCODINGS_WITH_BOM.Select(x => x.Key).OrderByDescending(x => x.Length).ToArray();
		private static readonly int MAX_BOM_LEN = ENCODINGS_WITH_BOM.Max(x => x.Key.Length);

		private const int HeadTailSize = 32;
		private const int MaxBufferSize = 128;

		private readonly string _filePath;
		private readonly Encoding _defaultEncoding;
		private FileStream _stream;
		private Encoding _encoding;
		private byte[] _byteBuffer;
		private ulong _bytesRead;
		private char[] _charBuffer = new char[MaxBufferSize];
		private ulong _charsRead;
		private ulong _fileInode;
		private ulong _initialPosition;
		private bool _checkFileRotation = false;
		private DateTime _lastReadOn;
		private CircularBuffer<byte> _headBytes = new CircularBuffer<byte>(HeadTailSize);
		private CircularBuffer<byte> _tailBytes = new CircularBuffer<byte>(HeadTailSize);
		private bool _disposed = false;
		#endregion

		#region Properties
		public string FilePath { get { return _filePath; } }
		public ulong FileInode { get { return _fileInode; } }
		public ulong CurrentPosition { get { return _bytesRead; } }
		public byte[] CurrentHeader { get { return _headBytes.ToArray(); } }
		public byte[] CurrentTail { get { return _tailBytes.ToArray(); } }
		public Encoding CurrentEncoding { get { return _encoding; } }
		#endregion

		#region .ctor
		public FileSourceReader(string filePath, Encoding encoding,
			ulong position = 0, ulong inode = 0,
			byte[] head = null, byte[] tail = null)
		{
			_filePath = filePath;
			_defaultEncoding = encoding ?? DEFAULT_ENCODING;
			_initialPosition = position;

			if (_initialPosition > 0)
			{
				_fileInode = inode;
				if (head != null) head.ForEach(x => _headBytes.Enqueue(x));
				if (tail != null) tail.ForEach(x => _tailBytes.Enqueue(x));
				_checkFileRotation = true;
			}

			_Log.InfoFormat("New reader for file: {0}. (encoding {1}; offset: {2}; inode: {3})", filePath, encoding.IfNotNull(x => x.EncodingName), position, inode);
		}
		#endregion

		#region Internal file stream handling

		private static FileStream TryCreateFileStream(string file)
		{
			try
			{
				var share = FileShare.ReadWrite | FileShare.Delete;
				return new FileStream(file, FileMode.Open, FileAccess.Read, share, 4096, FileOptions.SequentialScan);
			}
			catch (Exception ex)
			{
				_Log.WarnFormat("Unable to open file: {0}", ex, file);
			}

			return null;
		}

		// OPTIMIZE: Pass byte[] to write to by ref, to avoid allocating a new buffer on each call.
		private static byte[] ReadByteRangeFrom(FileStream stream, long offset, int count)
		{
			Guard.Against<ArgumentOutOfRangeException>((offset + count) > stream.Length, "(offset + count) > stream.Length");

			var save_offset = stream.Position;

			try
			{
				var len = Math.Min(stream.Length, count);
				var result = new byte[len];
				stream.Position = offset;
				stream.Read(result, 0, (int)len);
				return result;
			}
			finally
			{
				// restore stream's position.
				stream.Position = save_offset;
			}
		}

		private static Encoding TryGetEncodingFor(byte[] bom)
		{
			return BOM_PREAMBLES
				.FirstOrDefault(x => Helper.ArraysEqual(bom, x, x.Length))
				.IfNotNull(x => ENCODINGS_WITH_BOM[x]);
		}

		private static Encoding TryGuessEncodingFrom(FileStream stream)
		{
			var len = Math.Min(MAX_BOM_LEN, stream.Length);
			var preamble = ReadByteRangeFrom(stream, 0, (int)len);
			return (preamble != null && preamble.Length > 0) ? TryGetEncodingFor(preamble) : null;
		}

		private static ulong TryGetFileInode(FileInfo fi)
		{
			try
			{
				return fi.GetFileInodeNumber();
			}
			catch (Exception ex)
			{
				_Log.WarnFormat("Unable to get inode for file: {0}", ex, fi.FullName);
			}

			return 0;
		}

		private void TryOpenStream()
		{
			Guard.Against<InvalidOperationException>(_stream != null, "_stream != null");

			if ((_stream = TryCreateFileStream(_filePath)) != null)
			{
				// Try to guess encoding by reading file's preamble.
				// Or if no encoding was guessed, fallback to default.
				_encoding = /*TryGuessEncodingFrom(_stream) ?? */ _defaultEncoding;
				_byteBuffer = new byte[Math.Max(16, _defaultEncoding.GetMaxByteCount(1))];
				_bytesRead = _initialPosition;
				_charBuffer = new char[MaxBufferSize];
				_charsRead = 0;

				// If an initial position was requested, try to seek there (if possible)
				if (_initialPosition > 0 && !StreamHasRotated())
				{
					_stream.Seek((long)_bytesRead, SeekOrigin.Begin);
					_bytesRead = _initialPosition;
					if (_fileInode == 0) _stream.GetFileInodeNumber();
				}
				else
				{
					_bytesRead = 0;
					_fileInode = _stream.GetFileInodeNumber();
					_headBytes.Reset();
					_tailBytes.Reset();
				}

				_checkFileRotation = false;
				_initialPosition = 0;

				_Log.InfoFormat("Opened stream for file: {0}. (encoding {1}; offset: {2}; inode: {3})",
					_filePath, _encoding.IfNotNull(x => x.EncodingName), _bytesRead, _fileInode
				);
			}
		}

		private void CloseStream()
		{
			if (_stream != null)
			{
				_Log.DebugFormat("Closing stream for: {0}", _filePath);
				_stream.Close();
				_stream.Dispose();
				_stream = null;
			}
		}

		private unsafe bool StreamHasRotated()
		{
			if (_stream == null) return false;
			if (!_checkFileRotation) return false;

			if (CurrentPosition > (ulong)_stream.Length)
			{
				_Log.InfoFormat("Rotation detected on: {0} (reason: file size decreased).", _filePath);
				return true;
			}

			var fi = new FileInfo(_filePath);
			if (_fileInode != 0 && fi.Exists && (_fileInode != TryGetFileInode(fi)))
			{
				_Log.InfoFormat("Rotation detected on: {0} (reason: file inode changed).", _filePath);
				return true;
			}

			// If file has been written since our last read, file could
			// had been overwritten with more data than current length.
			// As a workaround we we'll check head/tail bytes against saved values.
			if (Helper.GetPreciseLastWriteTimeUtc(_filePath) > _lastReadOn)
			{
				// If current file was smaller than head/tail buffer, 
				// compare as much bytes as available from stream/file.
				var head = _headBytes.ToArray();
				//var head = stackalloc byte[_headBytes.UnsafeCount];
				var len = Math.Min((long)_headBytes.Count, _stream.Length);
				var bytes = ReadByteRangeFrom(_stream, 0, (int)len);
				if (len > 0 && !Helper.ArraysEqual(head, bytes, (int)len))
				{
					_Log.InfoFormat("Rotation detected on: {0} (reason: file header changed).", _filePath);
					return true;
				}
				// If header bytes matched, let's try to verify against tail bytes.
				else if (CurrentPosition > HeadTailSize)
				{
					var tail = _tailBytes.ToArray();
					var offset = CurrentPosition - (ulong)tail.Length;
					bytes = ReadByteRangeFrom(_stream, (long)offset, tail.Length);
					if (bytes.Length > 0 && !Helper.ArraysEqual(tail, bytes, bytes.Length))
					{
						_Log.InfoFormat("Rotation detected on: {0} (reason: file tail changed).", _filePath);
						return true;
					}
				}
			}

			return false;
		}

		private FileStream GetSourceStream()
		{
			Guard.Against<InvalidOperationException>(_disposed, "Reader has been disposed.");

			if (StreamHasRotated()) CloseStream();
			if (_stream == null) TryOpenStream();

			return _stream;
		}

		#endregion

		#region Char/Bytes Reading logic
		// Ensures that _buffer is at least length bytes
		// long, growing it if necessary
		private static void ResizeBufferAsNeeded(ref byte[] buffer, int length)
		{
			if (buffer.Length <= length)
			{
				byte[] new_buffer = new byte[length * 2];
				Buffer.BlockCopy(buffer, 0, new_buffer, 0, buffer.Length);
				buffer = new_buffer;
			}
		}

		private static int ReadCharBytes(FileStream stream, ref Encoding encoding, string filePath,
			ref char[] charbuf, int cindex, int ccount, ref byte[] bytebuf, out int bytes_read)
		{
			Encoding guessedEncoding = null;
			int chars_read = 0, pos = 0;
			bytes_read = 0;

			// Try to guess encoding by reading file's preamble..
			if (stream.Position == 0 && (guessedEncoding = TryGuessEncodingFrom(stream)) != null)
			{
				if (guessedEncoding != encoding)
				{
					_Log.DebugFormat("Detected encoding {0} for file: {1}.", guessedEncoding.EncodingName, filePath);
					encoding = guessedEncoding;
				}
			}

			byte[] preamble = stream.Position <= MAX_BOM_LEN ? encoding.GetPreamble() : null; // TODO: auto-detect encoding..

			while (chars_read < ccount)
			{
				int bindex = pos;

				while (true)
				{
					ResizeBufferAsNeeded(ref bytebuf, pos + 1);

					int read_byte = stream.ReadByte();
					if (read_byte == -1) return chars_read; // EOF

					bytebuf[pos++] = (byte)read_byte;
					bytes_read++;

					// If we are at the veri beggining of file, 
					// try to match preamble as expected.
					if (preamble != null && stream.Position <= preamble.Length)
					{
						if (Helper.ArraysEqual(preamble, bytebuf, bytes_read))
						{
							if (bytes_read < preamble.Length) continue;
							else break; // discard current buffer and keep reading.
						}
#if false
						// If preamble does not match expected, try auto-detection logic.
						else if ((guessedEncoding = TryGuessEncodingFrom(stream)) != null)
						{
							if (guessedEncoding != encoding)
							{
								_Log.DebugFormat("Detected encoding {0} for file: {1}.", guessedEncoding.EncodingName, filePath);
								encoding = guessedEncoding;
							}
							break;
						}
#endif
						else
						{
							_Log.WarnFormat("Unexpected file BOM detected while reading file: {0} (BOM: {1})",
								filePath, bytebuf.Take(bytes_read).ToArray().ToHexString());
							preamble = null; // Avoid any more preamble checks..
						}
					}

					int n = encoding.GetChars(bytebuf, bindex, (pos - bindex), charbuf, cindex + chars_read);
					// HACK: If we found the Unicode 'replacement character', we should 
					//		 keep fetching more bytes until a real character is found. (pruiz)
					if (n > 0 && charbuf[cindex] != '\uFFFD')
					{
						chars_read++;
						break;
					}
				}
			}

			return chars_read;
		}
		#endregion

		private int Read(int count, bool peek = false)
		{
			if (count < 0) throw new ArgumentOutOfRangeException("count is less than 0");

			int bytes_read;
			var stream = GetSourceStream();

			// If no stream available, we're done.
			if (stream == null) return 0;

			int chars_read = ReadCharBytes(stream, ref _encoding, _filePath, ref _charBuffer, 0, count, ref _byteBuffer, out bytes_read);

			if (peek)
			{
				// Reposition the stream
				_stream.Position -= bytes_read;
			}
			else
			{
				// Fill-in head-bytes buffer..
				if (CurrentPosition < HeadTailSize)
				{
					if (bytes_read == 1) _headBytes.Enqueue(_byteBuffer[0]);
					else _byteBuffer.Take(bytes_read).ForEach(x => _headBytes.Enqueue(x));
				}

				_bytesRead += (ulong)bytes_read;
				_charsRead += (ulong)chars_read;

				// fill-in tail-bytes buffer..
				if (bytes_read == 1) _tailBytes.Enqueue(_byteBuffer[0]);
				// OPTIMIZE: Pass all bytes at once to avoid multiple enqueue calls.
				else _byteBuffer.Take(bytes_read).ForEach(x => _tailBytes.Enqueue(x));
			}

			_lastReadOn = DateTime.UtcNow;

			return chars_read;
		}

		private char? ReadChar()
		{
			int count = Read(1);
			return count == 0 ? (char?)null : _charBuffer[0];
		}

		private char? PeekChar()
		{
			int count = Read(1, peek: true);
			// Return the single character we read or null if we read 0 characters
			return count == 0 ? (char?)null : _charBuffer[0];
		}

		private string ReadLineInternal()
		{
			char? ch;
			var sb = new StringBuilder();

			while ((ch = ReadChar()) != null)
			{
				sb.Append(ch);

				if (ch == '\n' || ch == '\r')
				{
					if (ch == '\r' && PeekChar() == '\n') ReadChar();
					return sb.ToString();
				}
			}

			return null;
		}

		public string ReadLine()
		{
			// Force opening of stream.
			var os = _stream ?? GetSourceStream();
			var br = _bytesRead;
			var cr = _charsRead;
			string result = null;

			// No stream available? Ok, we're done.
			if (_stream == null) return null;

			try
			{
				result = ReadLineInternal();

				// If no more data available, we will try to
				// detect file rotation on our next iteration.
				_checkFileRotation = result == null;
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				if (result == null)
				{
					// Nothing read, or incomplete line detected.
					// Reset counters to it's initial value.
					if (_stream != null && (os == null || os == _stream))
						_stream.Position -= (long)(_bytesRead - br);
					_bytesRead = br;
					_charsRead = cr;
				}
			}

			return result;
		}

		#region IDisposable Members

		public void Dispose()
		{
			CloseStream();
			_disposed = true;
		}

		#endregion
	}
}
