namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class LeasingSettings
    {
        public bool IsLeasingProvider { get; set; }
        public long LeasingAmountForOneTx { get; set; }
        public int LeasingSharePercent { get; set; }
        public string NodeAddress { get; set; }
    }
}