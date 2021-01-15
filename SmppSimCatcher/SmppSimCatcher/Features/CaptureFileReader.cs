using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using SmppSimCatcher.Model;
using SmppSimCatcher.Facilities;
using HermaFx.ComponentModel;
using System.ComponentModel;

namespace SmppSimCatcher.Features
{
	public class CaptureFileReader : BackgroundService, IHostedService, IDisposable
	{
		#region Private variables
		private static readonly global::Common.Logging.ILog _Log = global::Common.Logging.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private int _monitoringStep = 0;
		private TimeSpan[] _monitoringDelays;
		private FileSourceReader _reader = null;
		private CaptureLineParser _parser = new CaptureLineParser();
		private ConcurrentBag<SubmitSmPdu> _collection = new ConcurrentBag<SubmitSmPdu>();

		#endregion

		public IEnumerable<SubmitSmPdu> Collection => _collection;

		public CaptureFileReader(Settings settings)
		{
			_reader = new FileSourceReader(settings.CaptureFile, Encoding.UTF8);
			_monitoringDelays = (TimeSpan[])new StringArrayConverter<TimeSpan, TimeSpanConverter>(",", StringSplitOptions.RemoveEmptyEntries)
					.ConvertFromString(settings.MonitoringDelays);
		}
		private TimeSpan GetDelayForStep(int step, TimeSpan[] delays)
		{
			if (step == 0)
				return delays.First();
			else
				return step >= delays.Length ? delays.Last() : delays[step];
		}

		private async Task ExecuteAsyncInternal(CancellationToken stoppingToken)
		{
			string line = null;

			try
			{
				line = _reader.ReadLine();
			}
			catch (Exception ex)
			{
				_Log.ErrorFormat("Error while reading lines from {0}.", ex, _reader.FilePath);
			}

			if (line == null)
			{
				_monitoringStep++;
				var delay = GetDelayForStep(_monitoringStep, _monitoringDelays);
				await Task.Delay(delay, stoppingToken);
				return;
			}

			_monitoringStep = 0;

			try
			{
				var pdu = _parser.Parse(line);
				if (pdu != null)
				{
					_collection.Add(pdu);
				}
			}
			catch (Exception ex)
			{
				_Log.ErrorFormat("Error while parsing PDU from {0}: {1}", ex, _reader.FilePath, line);
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				await ExecuteAsyncInternal(stoppingToken);
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			_reader?.Dispose();
			_collection.Clear();
		}
	}
}