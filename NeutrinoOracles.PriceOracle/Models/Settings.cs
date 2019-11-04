namespace NeutrinoOracles.PriceOracle.Models
{
    public class Settings
    {
        public int TimeoutSec { get; set; }
        public string NodeUrl { get; set; }
        public char ChainId { get; set; }
        public string ContractAddress { get; set; }
    }
}