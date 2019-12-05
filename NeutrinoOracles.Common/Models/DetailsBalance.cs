namespace NeutrinoOracles.Common.Models
{
    public class DetailsBalance
    {
        public string Address { get; set; }
        public long Regular { get; set; }
        public long Generating { get; set; }
        public long Available { get; set; }
        public long Effective { get; set; }
    }
}