using System;
using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class AuctionAccountState
    {
        [AccountDataConvertInfo("order_first")]
        public string OrderFirst { get; set; }
        
        [AccountDataConvertInfo("order_next")]  
        public Dictionary<string, string> NextOrderByOrder { get; set; }
        
        [AccountDataConvertInfo("debug_order_roi_")]
        public Dictionary<string, long> RoiByOrder { get; set; }
        
        [AccountDataConvertInfo("order_total_")]
        public Dictionary<string, long> TotalByOrder { get; set; }
        
        [AccountDataConvertInfo("order_filled_total_")]  
        public Dictionary<string, long> FilledTotalByOrder { get; set; }
        
        [AccountDataConvertInfo("order_price_")]  
        public Dictionary<string, long> PriceByOrder { get; set; }
    }
}