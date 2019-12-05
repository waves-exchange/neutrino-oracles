using System;
using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class OracleAccountData
    {
        [AccountDataConvertInfo("price_")]
        public Dictionary<string, long> PriceByHeight { get; set; }
    }
}