using System;
using System.Text.RegularExpressions;

namespace SmppSimCatcher
{
	public static class StringHelper
	{
		private static readonly global::Common.Logging.ILog _Log = global::Common.Logging.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		public static string FromHexString(string hexString)
		{
			try
			{
				string ascii = string.Empty;

				for (int i = 2; i < hexString.Length-1; i += 2)
				{
					var hs = string.Empty;

					hs = hexString.Substring(i, 2);
					uint decval = Convert.ToUInt32(hs, 16);
					char character = Convert.ToChar(decval);
					ascii += character;
				}

				return ascii;
			}
			catch (Exception ex)
			{
				_Log.Error(ex.Message);
			}

			return string.Empty;
		}

		public static TimeSpan[] GetTimespanArrayFromString(string delays)
		{
			return Array.ConvertAll(delays.Split(',', StringSplitOptions.RemoveEmptyEntries), x => TimeSpan.Parse(x));
		}
	}
}
