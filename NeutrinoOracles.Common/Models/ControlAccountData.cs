using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class ControlAccountData
    {
        [AccountDataConvertInfo("oracles")]
        public string Oracles { get; set; }
        
        [AccountDataConvertInfo("price")]
        public long Price { get; set; }

        [AccountDataConvertInfo("price_")]
        public Dictionary<string, long> PriceByHeight { get; set; }
        
        [AccountDataConvertInfo("price_index_")]
        public Dictionary<string, long> PriceHeightByIndex { get; set; }
        
        [AccountDataConvertInfo("coefficient_oracle")]
        public long BftCoefficientOracle { get; set; }
    }
}