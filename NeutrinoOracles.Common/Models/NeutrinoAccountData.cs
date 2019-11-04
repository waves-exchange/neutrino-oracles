using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class NeutrinoAccountData
    {
        [AccountDataConvertInfo("control_contract")]
        public string ControlContractAddress { get; set; }
        
        [AccountDataConvertInfo("auction_contract")]
        public string AuctionContractAddress { get; set; }
        
        [AccountDataConvertInfo("neutrino_asset_id")]
        public string NeutrinoAssetId { get; set; } 
        
        [AccountDataConvertInfo("bond_asset_id")]
        public string BondAssetId { get; set; } 
        
        [AccountDataConvertInfo("swap_neutrino_locked_balance")]
        public long SwapNeutrinoLockedBalance { get; set; }
        
        [AccountDataConvertInfo("swap_locked_balance")]
        public long SwapWavesLockedBalance { get; set; }
        
        [AccountDataConvertInfo("orderbook")]
        public string Orderbook { get; set; }
        
        [AccountDataConvertInfo("order_total_")]
        public Dictionary<string, long> TotalByOrder { get; set; }
        
        [AccountDataConvertInfo("order_filled_total_")]  
        public Dictionary<string, long> FilledTotalByOrder { get; set; }
        
        [AccountDataConvertInfo("neutrino_")]  
        public Dictionary<string, long> WithdrawBalanceLockedByAddress { get; set; }
        
        [AccountDataConvertInfo("balance_block_")]  
        public Dictionary<string, long> WithdrawBalanceUnlockBlockByAddress { get; set; }
    }
}