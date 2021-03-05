using System.Collections.Generic;

namespace SmppSimCatcher.Model
{
	public class SubmitSmPdu
	{
		public uint CommandId { get; set; }
		public uint CommandStatus { get; set; }
		public uint SequenceNumber { get; set; }
		public string? ServiceType { get; set; }
		public byte SourceAddressTON { get; set; }
		public byte SourceAddressNPI { get; set; }
		public string SourceAddress { get; set; }
		public int DestinationAddressTON { get; set; }
		public int DestinationAddressNPI { get; set; }
		public string DestinationAddress { get; set; }
		public int ESMClass { get; set; }
		public int ProtocolId { get; set; }
		public int PriorityFlag { get; set; }
		public string? ScheduleDeliveryTime { get; set; }
		public string ValidityPeriod { get; set; }
		public int RegisteredDelivery { get; set; }
		public int ReplaceIfPresentFlag { get; set; }
		public int DataCoding { get; set; }
		public int ShortMessageDefaultMsgID { get; set; }
		public string ShortMessage { get; set; }

		public string Message { get; set; }
		public IDictionary<int, string> TlvParams { get; set; }
	}
}