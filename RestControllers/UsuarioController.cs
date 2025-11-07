using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using esupplier.Models;
using esupplier.Models.Response;
using esupplier.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace esupplier.ResControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private IUsuarioService _UsuarioService;
        private readonly IConfiguration _config;
        public UsuarioController(IUsuarioService usuarioService, IConfiguration config)
        {
            _UsuarioService = usuarioService;
            _config = config;
        }

        [HttpPost("validarUsuario")]
        public ActionResult validarUsuario()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _UsuarioService.validarUsuario(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("listarUsuario")]
        public ActionResult listarUsuario()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _UsuarioService.listarUsuario(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\",\"data\":{" + msj.data + "}}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }

        [HttpPost("mantenerUsuario")]
        public ActionResult mantenerUsuario()
        {
            string rpta = "";
            try
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string content = reader.ReadToEndAsync().Result.ToString();
                JObject jObject = JObject.Parse(content);

                Mensaje msj = new Mensaje();
                msj = _UsuarioService.mantenerUsuario(jObject);

                if (msj.tipo.Equals("S"))
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"S\"}";
                }
                else
                {
                    rpta = "{\"message\":\"" + msj.mensaje + "\",\"type\":\"E\"}";
                }

            }
            catch (Exception e)
            {
                rpta = "{\"message\":\"" + e.ToString() + "\",\"type\":\"E\"}";
            }

            return Ok(rpta);
        }
        /*
        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginModel login)
        {
            IActionResult response = Unauthorized();
            var user = AuthenticateUser(login);

            if (user != null)
            {
                var tokenString = GenerateJWTToken(user);
                response = Ok(new { token = tokenString });
            }

            return response;
        }

        private UsuarioModel AuthenticateUser(LoginModel login)
        {
            // Aquí deberías autenticar al usuario, por ejemplo, consultando una base de datos o algún otro medio.
            // En este ejemplo, solo estamos comprobando si las credenciales son "admin" y "admin".
            if (login.usuario == "admin" && login.clave == "admin")
            {
                return new UsuarioModel { id =1, usuario = "admin" };
            }
            return null;
        }

        private string GenerateJWTToken(UsuarioModel user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
        {
            new Claim("usuario", user.usuario),
            new Claim("id", ""+user.id),
            // Puedes añadir más claims según necesites.
        };

            var token = new JwtSecurityToken(_config["Jwt:Issuer"],
              _config["Jwt:Issuer"],
              claims,
              expires: DateTime.Now.AddMinutes(30),
              signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }*/
    }
}
