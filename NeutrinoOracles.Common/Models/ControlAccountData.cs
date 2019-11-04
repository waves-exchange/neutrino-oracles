using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class ControlAccountData
    {
        [AccountDataConvertInfo("price")]
        public long Price { get; set; }
        
        [AccountDataConvertInfo("providing_expire_block")]
        public long ProvidingExpireBlock { get; set; }
        
        [AccountDataConvertInfo("is_pending_price")]
        public bool IsPendingPrice { get; set; }
        
        [AccountDataConvertInfo("oracle_is_provide_")]
        public Dictionary<string, bool> IsProvidedByOracle { get; set; }
        
        [AccountDataConvertInfo("price_index_")]
        public Dictionary<string, long> PriceHeightByIndex { get; set; }
    }
}