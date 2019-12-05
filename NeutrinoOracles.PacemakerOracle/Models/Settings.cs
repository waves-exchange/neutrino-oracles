namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class Settings
    {
        public char ChainId { get; set; }
        public string NodeUrl { get; set; }
        public string ContractPubKey { get; set; }
        public int TimeoutSec { get; set; }
        public LeasingSetting Leasing { get; set; }
    }

    public class LeasingSetting
    {
        public bool IsLeasingProvider { get; set; }
        public long LeasingAmountForOneTx { get; set; }
        public int LeasingSharePercent { get; set; }
        public string NodeAddress { get; set; }
    }
}