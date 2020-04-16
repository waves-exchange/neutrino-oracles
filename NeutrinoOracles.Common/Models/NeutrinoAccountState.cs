using System.Collections.Generic;
using NeutrinoOracles.Common.Attributes;

namespace NeutrinoOracles.Common.Models
{
    public class NeutrinoAccountState
    {
        [AccountDataConvertInfo("liquidation_contract")]
        public string LiquidationContract { get; set; } 
        
        [AccountDataConvertInfo("control_contract")]
        public string ControlContract { get; set; } 
        
        [AccountDataConvertInfo("auction_contract")]
        public string AuctionContract { get; set; } 
        
        [AccountDataConvertInfo("neutrino_asset_id")]
        public string NeutrinoAssetId { get; set; }

        [AccountDataConvertInfo("balance_lock_waves_")]  
        public Dictionary<string, long> BalanceLockWavesByUser { get; set; }
        
        [AccountDataConvertInfo("balance_lock_waves")]  
        public long BalanceLockWaves { get; set; }
        
        [AccountDataConvertInfo("balance_lock_neutrino_")]  
        public Dictionary<string, long> BalanceLockNeutrinoByUser { get; set; }
        
        [AccountDataConvertInfo("balance_lock_neutrino")]  
        public long BalanceLockNeutrino { get; set; }
        
        [AccountDataConvertInfo("balance_unlock_block_")]  
        public Dictionary<string, long> BalanceUnlockBlockByAddress { get; set; }
    }
}