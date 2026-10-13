using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using esupplier.Models;
using Microsoft.IdentityModel.Tokens;

namespace esupplier.Services.Auth
{
    /// <summary>
    /// Emite el JWT del login con los mismos Issuer/Audience/clave que valida Startup.
    /// </summary>
    public static class GeneradorTokenJwt
    {
        public static bool EstaConfigurado(IConfiguration config)
        {
            return !string.IsNullOrWhiteSpace(config["Jwt:Key"]);
        }

        public static (string token, DateTime expiraEnUtc) Generar(IConfiguration config, ResultadoLogin login)
        {
            var credenciales = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"])),
                SecurityAlgorithms.HmacSha256);

            int minutos = config.GetValue<int?>("Jwt:ExpiracionMinutos") ?? 480;
            if (minutos <= 0) minutos = 480;

            string rolId = login.RolId.ToString(CultureInfo.InvariantCulture);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, login.Usuario ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("usuario", login.Usuario ?? ""),
                new Claim("reg_id", login.RegId.ToString(CultureInfo.InvariantCulture)),
                new Claim("rol_id", rolId),
                new Claim(ClaimTypes.Role, rolId),
                new Claim("adm_cliente_id", login.AdmClienteId ?? ""),
                new Claim("codigo_sociedad", login.CodigoSociedad ?? ""),
                new Claim("codigo_referencia", login.CodigoReferencia ?? "")
            };

            DateTime ahoraUtc = DateTime.UtcNow;
            DateTime expira = ahoraUtc.AddMinutes(minutos);

            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"] ?? "consenso-api",
                audience: config["Jwt:Audience"] ?? "consenso-web",
                claims: claims,
                notBefore: ahoraUtc,
                expires: expira,
                signingCredentials: credenciales);

            return (new JwtSecurityTokenHandler().WriteToken(token), expira);
        }
    }
}
