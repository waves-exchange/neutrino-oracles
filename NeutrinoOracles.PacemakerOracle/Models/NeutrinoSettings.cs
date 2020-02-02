namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class NeutrinoSettings
    {
        public string NeutrinoPubKeyBase58 { get; set; }
        public string NeutrinoAddress { get; set; }
        public string AuctionAddress { get; set; }
        public string LiquidationAddress { get; set; }
        public string ControlAddress { get; set; }
    }
}