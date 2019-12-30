using System.Collections.Generic;

namespace NeutrinoOracles.Common.Models
{
    public class ActiveLeaseTx
    {
        public string SenderPublicKey { get; set; }
        public long Amount { get; set; }
        public string Sender { get; set; }
        public string FeeAssetId { get; set; }
        public List<string> Proofs { get; set; }
        public int Fee { get; set; }
        public string Recipient { get; set; }
        public string Id { get; set; }
        public int Type { get; set; }
        public int Version { get; set; }
        public long Timestamp { get; set; }
        public int Height { get; set; }
        public string Signature { get; set; }
    }
}