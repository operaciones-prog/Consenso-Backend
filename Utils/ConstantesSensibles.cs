namespace esupplier.Utils
{
    /// <summary>
    /// Define qué constantes de cfg_constante son secretos y no deben devolverse por la API.
    /// </summary>
    public static class ConstantesSensibles
    {
        public const string Mascara = "********";

        private static readonly HashSet<string> CodigosSensibles = new(StringComparer.OrdinalIgnoreCase)
        {
            "PASS", "PASSWORD", "CLAVE", "SECRECT", "SECRET", "CLIENT_SECRET", "KEY", "TOKEN"
        };

        public static bool EsSensible(string tipoConstante, string codigoConstante)
        {
            return !string.IsNullOrEmpty(codigoConstante) && CodigosSensibles.Contains(codigoConstante.Trim());
        }
    }
}
