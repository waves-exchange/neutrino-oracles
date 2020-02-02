namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class InitPacemakerInfo
    {
        public long TotalNeutrinoSupply { get; set; }
        public long NeutrinoBalance { get; set; }
        public long WavesBalance { get; set; }
        public long Height { get; set; }
        public long LiquidationNeutrinoBalance { get; set; }
        public long AuctionBondBalance { get; set; }
        public long Supply { get; set; }
        public long Reserve { get; set; }
        public long Deficit { get; set; }
    }
}