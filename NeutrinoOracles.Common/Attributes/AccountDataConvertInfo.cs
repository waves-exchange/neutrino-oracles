using System;

namespace NeutrinoOracles.Common.Attributes
{
    public class AccountDataConvertInfo : Attribute
    {
        public string Key { get; }
        public AccountDataConvertInfo(string key)
        {
            Key = key;
        }
    }
}