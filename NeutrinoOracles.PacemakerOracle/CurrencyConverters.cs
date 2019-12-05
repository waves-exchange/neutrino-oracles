namespace NeutrinoOracles.PacemakerOracle
{
    public static class CurrencyConvert
    {
        public const int Wavelet = 100000000;
        public const int Pauli = 1000000;
        public static long NeutrinoToWaves(long amount, long price) => amount * 100 / price * Wavelet / Pauli;
        public static long WavesToNeutrino(long amount, long price) => amount * price / 100 * Pauli / Wavelet;
        public static long NeutrinoToBond(long amount) => amount / Pauli;
        public static long BondToNeutrino(long amount) => amount * Pauli;
        public static long WavesToBond(long amount, long price) => NeutrinoToBond(WavesToNeutrino(amount, price));

    }
}