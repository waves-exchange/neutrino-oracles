using System;
using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class LiquidationAccountData
    {
        [AccountDataConvertInfo("order_first")]
        public string OrderFirst { get; set; }
        
        [AccountDataConvertInfo("order_last")]
        public string OrderLast { get; set; }
        
        [AccountDataConvertInfo("order_total_")]
        public Dictionary<string, long> TotalByOrder { get; set; }
        
        [AccountDataConvertInfo("order_filled_total_")]  
        public Dictionary<string, long> FilledTotalByOrder { get; set; }
        
        [AccountDataConvertInfo("order_next")]  
        public Dictionary<string, string> NextOrderByOrder { get; set; }
    }
}