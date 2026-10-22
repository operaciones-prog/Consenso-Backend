namespace esupplier.Services.IA
{
    /// <summary>Tokens de una o más llamadas a Claude y su costo calculado.</summary>
    public class UsoIA
    {
        public string modelo { get; set; } = "";
        /// <summary>Tokens de entrada sin caché.</summary>
        public long tokens_entrada { get; set; }
        public long tokens_salida { get; set; }
        public long tokens_cache_escritura { get; set; }
        public long tokens_cache_lectura { get; set; }
        /// <summary>true = se pagó con el saldo (clave de la plataforma).</summary>
        public bool con_saldo { get; set; }
        public decimal costo_usd { get; set; }

        /// <summary>Entrada total que se guarda en tb_ia_uso (con caché).</summary>
        public long EntradaTotal => tokens_entrada + tokens_cache_escritura + tokens_cache_lectura;
    }

    /// <summary>
    /// Precios de la API de Anthropic en US$ por millón de tokens (lista pública, sin descuentos).
    /// Escribir en caché cuesta 1,25× la entrada y leer de caché 0,1×.
    /// Si Anthropic cambia sus precios, actualizar esta tabla.
    /// </summary>
    public static class PreciosIA
    {
        public const decimal FactorCacheEscritura = 1.25m;
        public const decimal FactorCacheLectura = 0.10m;

        public static readonly IReadOnlyDictionary<string, (decimal entrada, decimal salida)> PorMillon =
            new Dictionary<string, (decimal entrada, decimal salida)>
            {
                ["claude-opus-5-5"] = (4m, 20m),
                ["claude-sonnet-5-5"] = (2m, 10m),
                ["claude-haiku-5-5"] = (0.10m, 0.50m),
            };

        /// <summary>Modelo desconocido: se cobra como el más caro de la tabla, para no subestimar.</summary>
        private static (decimal entrada, decimal salida) Precio(string modelo) =>
            PorMillon.TryGetValue(modelo ?? "", out var p) ? p : (4m, 20m);

        public static decimal Calcular(UsoIA uso)
        {
            var (entrada, salida) = Precio(uso.modelo);
            decimal costo = (uso.tokens_entrada * entrada
                             + uso.tokens_cache_escritura * entrada * FactorCacheEscritura
                             + uso.tokens_cache_lectura * entrada * FactorCacheLectura
                             + uso.tokens_salida * salida) / 1_000_000m;
            return Math.Round(costo, 6);
        }

        /// <summary>Tabla para mostrar en pantalla.</summary>
        public static object Tabla() => PorMillon.Select(p => new
        {
            modelo = p.Key,
            entrada_por_millon = p.Value.entrada,
            salida_por_millon = p.Value.salida
        }).ToList();
    }
}
