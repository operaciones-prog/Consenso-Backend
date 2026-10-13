using System.Security.Cryptography;

namespace esupplier.Utils
{
    /// <summary>
    /// Hash de contraseñas con PBKDF2-SHA256.
    /// Formato almacenado: PBKDF2$iteraciones$saltBase64$hashBase64 (aprox. 90 caracteres).
    /// Soporta contraseñas heredadas en texto plano para la migración transparente:
    /// <see cref="Verificar"/> indica si hay que re-hashear.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefijo = "PBKDF2";
        private const int Iteraciones = 100_000;
        private const int TamanoSalt = 16;
        private const int TamanoHash = 32;

        public static string Hashear(string clave)
        {
            if (clave == null) throw new ArgumentNullException(nameof(clave));
            byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(clave, salt, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
            return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool EsHash(string valorAlmacenado)
        {
            return !string.IsNullOrEmpty(valorAlmacenado) && valorAlmacenado.StartsWith(Prefijo + "$", StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifica la clave contra el valor almacenado (hash o texto plano heredado).
        /// <paramref name="requiereRehash"/> es true cuando la clave coincidió con un valor heredado en texto plano.
        /// </summary>
        public static bool Verificar(string clave, string valorAlmacenado, out bool requiereRehash)
        {
            requiereRehash = false;
            if (clave == null || string.IsNullOrEmpty(valorAlmacenado)) return false;

            if (!EsHash(valorAlmacenado))
            {
                // Contraseña heredada en texto plano: comparación en tiempo constante y exacta (sensible a mayúsculas).
                bool coincide = CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(clave),
                    System.Text.Encoding.UTF8.GetBytes(valorAlmacenado));
                requiereRehash = coincide;
                return coincide;
            }

            string[] partes = valorAlmacenado.Split('$');
            if (partes.Length != 4 || !int.TryParse(partes[1], out int iteraciones)) return false;

            byte[] salt, esperado;
            try
            {
                salt = Convert.FromBase64String(partes[2]);
                esperado = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(clave, salt, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            bool valido = CryptographicOperations.FixedTimeEquals(calculado, esperado);
            requiereRehash = valido && iteraciones < Iteraciones;
            return valido;
        }

        /// <summary>Genera una contraseña aleatoria criptográficamente segura.</summary>
        public static string GenerarClaveAleatoria(int longitud = 10)
        {
            const string caracteres = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var resultado = new char[longitud];
            for (int i = 0; i < longitud; i++)
            {
                resultado[i] = caracteres[RandomNumberGenerator.GetInt32(caracteres.Length)];
            }
            return new string(resultado);
        }
    }
}
