namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class Settings
    {
        public char ChainId { get; set; }
        public string NodeUrl { get; set; }
        public long DeficitOffset { get; set; }
        public NeutrinoSettings NeutrinoSettings { get; set; }
        public int TimeoutSec { get; set; }
        public LeasingSettings Leasing { get; set; }
    }
}