namespace esupplier.Utils
{
    /// <summary>
    /// Fechas en hora de Ecuador. En AWS Lambda DateTime.Now es UTC, por eso no debe usarse directamente.
    /// </summary>
    public static class FechaUtil
    {
        private static readonly TimeZoneInfo ZonaEcuador = ObtenerZona();

        private static TimeZoneInfo ObtenerZona()
        {
            foreach (var id in new[] { "America/Guayaquil", "SA Pacific Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            // Ecuador continental: UTC-5 sin horario de verano.
            return TimeZoneInfo.CreateCustomTimeZone("ECT", TimeSpan.FromHours(-5), "Ecuador", "Ecuador");
        }

        public static DateTime AhoraEcuador()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaEcuador);
        }
    }
}
