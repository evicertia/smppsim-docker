using System;
using System.Collections.Generic;
using System.Linq;

using SmppSimCatcher.Model;

namespace SmppSimCatcher.Features
{
	public class CaptureLineParser
	{
		private static SubmitSmPdu ParseSubmitSm(IEnumerable<string> parts)
		{
			var result = new SubmitSmPdu();

			IEnumerable<KeyValuePair<string, string>> kvs = parts.ToList()
				.Select(x => x.Split('='))
				.Select(x => new KeyValuePair<string, string>(x[0], x[1])).ToList();

			foreach (var item in kvs)
			{

				switch (item.Key)
				{
					case "cmd_id": result.CommandId = UInt32.Parse(item.Value); break;
					case "cmd_status": result.CommandStatus = UInt32.Parse(item.Value); break;
					case "seq_no": result.SequenceNumber = UInt32.Parse(item.Value); break;
					case "service_type": result.ServiceType = item.Value; break;
					case "source_addr_ton": result.SourceAddressTON = Byte.Parse(item.Value); break;
					case "source_addr_npi": result.SourceAddressNPI = Byte.Parse(item.Value); break;
					case "source_addr": result.SourceAddress = item.Value; break;
					case "dest_addr_ton": result.DestinationAddressTON = Byte.Parse(item.Value); break;
					case "dest_addr_npi": result.DestinationAddressNPI = Byte.Parse(item.Value); break;
					case "dest_addr": result.DestinationAddress = item.Value; break;
					case "esm_class": result.ESMClass = Byte.Parse(item.Value); break;
					case "protocol_ID": result.ProtocolId = Byte.Parse(item.Value); break;
					case "priority_flag":result.PriorityFlag = Byte.Parse(item.Value); break;
					case "schedule_delivery_time": result.ScheduleDeliveryTime = item.Value; break;
					case "validity_period": result.ValidityPeriod = item.Value; break;
					case "registered_delivery_flag": result.RegisteredDelivery = Byte.Parse(item.Value); break;
					case "replace_if_present_flag": result.ReplaceIfPresentFlag = Byte.Parse(item.Value); break;
					case "data_coding": result.DataCoding = Byte.Parse(item.Value); break;
					case "sm_default_msg_id": result.ShortMessageDefaultMsgID = Byte.Parse(item.Value); break;
					case "short_message": result.ShortMessage = item.Value; result.Message = item.Value; break;
					case "tag":
					case "len":
					case "value":
					default:
						break;
				}
			}

			Dictionary<int, string> tlvs = new Dictionary<int, string>();
			var startofsm = kvs.Select((x, index) => new { x, index }).First(x => x.x.Key == "tag").index;

			for (int i = startofsm; i < kvs.Count() - 2; i += 3)
			{
				var tag = int.Parse(kvs.ElementAt(i).Value); //< XXX: Tag is a hex encoded integer
				tlvs.Add(tag, kvs.ElementAt(i + 2).Value.TrimEnd(new[] { '\n', '\r' }));

				// Per-tag specifing parsing/deserialization.
				switch (tag)
				{
					case 1060: //< MessagePayload..
						result.Message = StringHelper.FromHexString(tlvs[1060]).Replace('\n', ' ').Replace('\r', ' ');
						break;
				}
			}
			result.TlvParams = tlvs;
			return result;
		}

		public SubmitSmPdu Parse(string line)
		{
			var parts = line.Split(',');

			// We're only interested on submitsm pdus right now..
			if (parts[1] != "cmd_id=4")
			{
				return null;
			}

			return ParseSubmitSm(parts);
		}
	}
}
