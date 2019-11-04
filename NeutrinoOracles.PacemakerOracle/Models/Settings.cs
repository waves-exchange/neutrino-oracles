namespace NeutrinoOracles.PacemakerOracle.Models
{
    public class Settings
    {
        public char ChainId { get; set; }
        public string NodeUrl { get; set; }
        public string ContractAddress { get; set; }
        public int TimeoutSec { get; set; }
    }
}