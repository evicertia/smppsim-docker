using HermaFx;
using HermaFx.Utils;
using Mono.Unix;
using Mono.Unix.Native;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SmppSimCatcher
{
	internal static class Helper
	{
		/// <summary>
		/// Run the specified @delegate avoiding throwing any exception (by catching & ignoring them if any).
		/// </summary>
		/// <param name="delegate">The @delegate.</param>
		public static void ShallowExceptions(Action @delegate)
		{
			try
			{
				@delegate();
			}
			catch
			{
			}
		}

		/// <summary>
		/// Convert the hex string to byte array.
		/// </summary>
		/// <param name="hexString">The hex string.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException"></exception>
		public static byte[] HexStringToByteArray(string hexString)
		{
			if (hexString.Length % 2 != 0)
			{
				throw new ArgumentException(String.Format("The binary key cannot have an odd number of digits: {0}", hexString));
			}

			byte[] HexAsBytes = new byte[hexString.Length / 2];
			for (int index = 0; index < HexAsBytes.Length; index++)
			{
				string byteValue = hexString.Substring(index * 2, 2);
				HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}

			return HexAsBytes;
		}

		public static string ToHumanReadableHexString(this byte[] @this)
		{
			if (@this == null) return null;

			return @this.Select(x => x.ToString("x2")).Aggregate((a, b) => a + ":" + b);
		}

		public static void Append(this Stream stream, byte value)
		{
			stream.Append(new[] { value });
		}

		public static void Append(this Stream stream, byte[] values)
		{
			stream.Write(values, 0, values.Length);
		}

		public static string ToIso8601(this DateTime date)
		{
			return date.ToUniversalTime()
				.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
		}

		public static ulong GetFileInodeNumber(this FileInfo fileInfo)
		{
			if (fileInfo == null) throw new ArgumentNullException("fileInfo");

			return EnvironmentHelper.RunningOnUnix ?
				PosixHelper.GetFileInodeNum(fileInfo)
				: WinAPIHelper.GetFileSystemIdFor(fileInfo);
		}

		public static ulong GetFileInodeNumber(this FileStream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			return EnvironmentHelper.RunningOnUnix ?
				PosixHelper.GetFileInodeNum(stream)
				: WinAPIHelper.GetFileSystemIdFor(stream);
		}

		public static DateTime GetPreciseLastWriteTimeUtc(string path)
		{
			return EnvironmentHelper.RunningOnUnix ?
				PosixHelper.GetPreciseLastWriteTimeUtc(path)
				: File.GetLastWriteTimeUtc(path);
		}

		public static long ExponentialDelay(int step, int maxDelayInSeconds = 1024)
		{
			//Attempt 1     0s     0s
			//Attempt 2     2s     2s
			//Attempt 3     4s     4s
			//Attempt 4     8s     8s
			//Attempt 5     16s    16s
			//Attempt 6     32s    32s

			//Attempt 7     64s     1m 4s
			//Attempt 8     128s    2m 8s
			//Attempt 9     256s    4m 16s
			//Attempt 10    512     8m 32s
			//Attempt 11    1024    17m 4s
			//Attempt 12    2048    34m 8s

			//Attempt 13    4096    1h 8m 16s
			//Attempt 14    8192    2h 16m 32s
			//Attempt 15    16384   4h 33m 4s

			var delayInSeconds = ((1d / 2d) * (Math.Pow(2d, step) - 1d));

			return maxDelayInSeconds < delayInSeconds
				? Convert.ToInt64(maxDelayInSeconds)
				: Convert.ToInt64(delayInSeconds);
		}

		public static int SteppedDelay(int step, int[] delaysInMiliseconds)
		{
			if (step == 0) throw new ArgumentOutOfRangeException("step == 0");
			if (delaysInMiliseconds == null || delaysInMiliseconds.Length == 0)
				throw new ArgumentNullException("delaysInMiliseconds");

			return step > delaysInMiliseconds.Length ? delaysInMiliseconds.Last() : delaysInMiliseconds[step - 1];
		}

		public static bool ArraysEqual(byte[] b1, byte[] b2, int len = 0)
		{
			Guard.IsNotNull(() => b1, b1);
			Guard.IsNotNull(() => b2, b2);
			Guard.Against<ArgumentOutOfRangeException>(len < 0, "len < 0");
			Guard.Against<ArgumentOutOfRangeException>(len > Math.Max(b1.Length, b2.Length), "len > Max(b1.Length, b2.Length)");

			unsafe
			{
				var min = Math.Min(b1.Length, b2.Length);

				if (len == 0 && (b1.Length != b2.Length))
					return false;
				else if (len != 0 && len > min)
					return false;

				int n = len != 0 ? len : min;

#if false
				int tmp = 0;
				while (n-- > 0)
				{
					var pos = tmp++;
					if (b1[pos] != b2[pos])
						return false;
				}
#else
				fixed (byte* p1 = b1, p2 = b2)
				{
					byte* ptr1 = p1;
					byte* ptr2 = p2;

					while (n-- > 0)
					{
						if (*ptr1++ != *ptr2++)
							return false;
					}
				}
#endif
				return true;
			}
		}

		public static unsafe bool ArraysEqual(byte* a, int alen, byte* b, int blen, int len = 0)
		{
			Guard.Against<ArgumentNullException>(a == null, "a");
			Guard.Against<ArgumentNullException>(b == null, "b");
			Guard.Against<ArgumentOutOfRangeException>(len < 0, "len < 0");
			Guard.Against<ArgumentOutOfRangeException>(len > Math.Max(alen, blen), "len > Max(alen, blen)");

			var min = Math.Min(alen, blen);

			if (len == 0 && (alen != blen)) return false;
			else if (len != 0 && len > min) return false;

			int n = len != 0 ? len : min;
			byte* ptr1 = a, ptr2 = b;

			while (n-- > 0)
			{
				if (*ptr1++ != *ptr2++)
					return false;
			}

			return true;
		}

	}

	public static class WinAPIHelper
	{
		#region GetFileSystemId
		#region structs and enums
		public struct IO_STATUS_BLOCK
		{
#pragma warning disable 169
			uint status;
			ulong information;
#pragma warning restore 169
		}
		public struct _FILE_INTERNAL_INFORMATION
		{
			public ulong IndexNumber;
		}

		// Abbreviated, there are more values than shown
		public enum FILE_INFORMATION_CLASS
		{
			FileDirectoryInformation = 1,     // 1
			FileFullDirectoryInformation,     // 2
			FileBothDirectoryInformation,     // 3
			FileBasicInformation,         // 4
			FileStandardInformation,      // 5
			FileInternalInformation      // 6
		}

		public struct BY_HANDLE_FILE_INFORMATION
		{
			public uint FileAttributes;
#pragma warning disable 618
			public FILETIME CreationTime;
			public FILETIME LastAccessTime;
			public FILETIME LastWriteTime;
#pragma warning restore 618
			public uint VolumeSerialNumber;
			public uint FileSizeHigh;
			public uint FileSizeLow;
			public uint NumberOfLinks;
			public uint FileIndexHigh;
			public uint FileIndexLow;
		}
		#endregion

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

		[DllImport("ntdll.dll", SetLastError = true)]
		public static extern IntPtr NtQueryInformationFile(IntPtr fileHandle, ref IO_STATUS_BLOCK IoStatusBlock, IntPtr pInfoBlock, uint length, FILE_INFORMATION_CLASS fileInformation);

		public static ulong GetFileSystemIdFor(FileStream fs)
		{
			if (fs == null) throw new ArgumentNullException("fs");
			//if (fs.Handle <= 0) throw new ArgumentException("file stream should have an associated handle");

			var objectFileInfo = new WinAPIHelper.BY_HANDLE_FILE_INFORMATION();
#pragma warning disable CS0618 // Type or member is obsolete
			WinAPIHelper.GetFileInformationByHandle(fs.Handle, out objectFileInfo); // FIXME: Avoid using deprecated handle property.
#pragma warning restore CS0618 // Type or member is obsolete
			var fileIndex = ((ulong)objectFileInfo.FileIndexHigh << 32) + (ulong)objectFileInfo.FileIndexLow;

			return fileIndex;
		}

		public static ulong GetFileSystemIdFor(FileInfo fi)
		{
			if (fi == null) throw new ArgumentNullException("fi");

			using (var stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				return GetFileSystemIdFor(stream);
			}
		}
		#endregion

	}
	public static class PosixHelper
	{
		#region GetFileInode
		public static ulong GetFileInodeNum(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentNullException("path");

			var statOut = new Stat();
			int statRet = Syscall.stat(path, out statOut);

			if (statRet == -1)
			{
				throw new IOException("stat on " + path + " failed.");
			}

			return statOut.st_ino;
		}

		public static ulong GetFileInodeNum(FileInfo fi)
		{
			if (fi == null) throw new ArgumentNullException("fi");
			return GetFileInodeNum(fi.FullName);
		}

		public static ulong GetFileInodeNum(FileStream fs)
		{
			if (fs == null) throw new ArgumentNullException("fs");
			return GetFileInodeNum(fs.Name);
		}
		#endregion

		#region GetPreciseLastWriteTimeUtc
		public static DateTime GetPreciseLastWriteTimeUtc(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentNullException("path");

			var statOut = new Stat();
			int statRet = Syscall.stat(path, out statOut);

			if (statRet == -1)
			{
				var errno = Syscall.GetLastError();
				if (errno == Errno.ESRCH || errno == Errno.ENOTDIR || errno == Errno.ENOENT)
				{
					return DateTime.MinValue;
				}

				UnixMarshal.ThrowExceptionForError(errno);
			}

			var result = NativeConvert.UnixEpoch.AddSeconds(statOut.st_mtime);

			// If this platform supports nanosecs 
			// precision, take it into account.
			if (statOut.st_mtime_nsec != default(long))
			{
				var ns = (double)statOut.st_mtime_nsec;
				result = result.AddSeconds(ns / 1000000000d);
			}

			return result;
		}
		#endregion
	}
}
