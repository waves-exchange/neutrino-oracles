using System;

namespace NeutrinoOracles.PacemakerOracle
{
    public static class CurrencyConvert
    {
        public const decimal Wavelet = 100000000;
        public const decimal Pauli = 1000000;
        public static long NeutrinoToWaves(long amount, long price) => (long) (Convert.ToDecimal(amount * 100 / price)/Pauli * Wavelet);
        public static long WavesToNeutrino(long amount, long price) =>  (long) (Convert.ToDecimal(amount * price / 100)/Wavelet * Pauli); 
        public static long NeutrinoToBond(long amount) => amount;
        public static long BondToNeutrino(long amount) => amount;
        public static long WavesToBond(long amount, long price) => NeutrinoToBond(WavesToNeutrino(amount, price));

    }
}