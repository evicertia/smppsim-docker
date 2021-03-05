using System;
using System.Collections.Generic;
using System.Linq;

using HermaFx;

using Microsoft.AspNetCore.Mvc;

using SmppSimCatcher.Model;
using SmppSimCatcher.Features;

namespace SmppSimCatcher.Controllers
{
	[ApiController]
	public class SmsController : ControllerBase
	{
		private static readonly global::Common.Logging.ILog _Log = global::Common.Logging.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly CaptureFileReader _reader;

		public SmsController(CaptureFileReader reader)
		{
			_reader = reader;
		}

		[HttpGet]
		[Route("/")]
		public SubmitSmPdu Index([FromQuery] uint id)
		{
			return _reader.Collection.FirstOrDefault(x => x.SequenceNumber == id);
		}

		[HttpGet]
		[Route("/list")]
		public IEnumerable<SubmitSmPdu> List([FromQuery] uint? limit, [FromQuery] uint? offset)
		{
			var result = _reader.Collection;

			offset.IfHasValue(x => result = result.Skip((int)x));
			limit.IfHasValue(x => result = result.Take((int)x));

			return result.ToArray();
		}

		[HttpGet]
		[Route("/search")]
		public IEnumerable<SubmitSmPdu> MessageByContent(
			[FromQuery] string q,
			[FromQuery] string source,
			[FromQuery] string destination,
			[FromQuery] string text,
			[FromQuery] uint? limit)
		{
			var result = _reader.Collection;

			q.IfNotNullOrWhiteSpace(x =>
				result = result.Where(o
					=> (bool)o.Message?.Contains(q)
					|| (bool)o.ShortMessage?.Contains(q)
					|| (bool)o.SourceAddress?.Contains(q)
					|| (bool)o.DestinationAddress?.Contains(q)
				)
			);
			source.IfNotNullOrWhiteSpace(x => result = result.Where(o => o.SourceAddress == x));
			destination.IfNotNullOrWhiteSpace(x => result = result.Where(o => o.DestinationAddress == x));
			text.IfNotNullOrWhiteSpace(x => result = result.Where(o => o.Message.Contains(x)));
			limit.IfHasValue(x => result = result.Take((int)limit));

			return result.ToArray();
		}
	}
}
