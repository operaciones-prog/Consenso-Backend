using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace esupplier.Services.IA
{
    /// <summary>Guarda las API keys de IA fuera de la base de datos.</summary>
    public interface IAlmacenSecretos
    {
        /// <summary>Crea o reemplaza el secreto.</summary>
        Task GuardarAsync(string nombre, string valor);

        /// <summary>Devuelve el valor, o null si el secreto no existe.</summary>
        Task<string> LeerAsync(string nombre);

        /// <summary>Programa el borrado (7 días de recuperación). No falla si no existe.</summary>
        Task EliminarAsync(string nombre);
    }

    /// <summary>
    /// AWS Secrets Manager. En Lambda usa el rol IAM de la función, que necesita
    /// secretsmanager:CreateSecret, PutSecretValue, GetSecretValue, RestoreSecret y DeleteSecret
    /// sobre "arn:aws:secretsmanager:*:*:secret:{IA:SecretPrefix}/*".
    /// En local usa la cadena de credenciales por defecto de AWS (perfil o variables de entorno).
    /// </summary>
    public class AlmacenSecretosAws : IAlmacenSecretos
    {
        private readonly IAmazonSecretsManager _cliente;

        public AlmacenSecretosAws(IConfiguration configuration)
        {
            string region = configuration["IA:Region"] ?? configuration["Storage:S3:Region"] ?? "us-east-1";
            _cliente = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
        }

        public async Task GuardarAsync(string nombre, string valor)
        {
            try
            {
                await _cliente.PutSecretValueAsync(new PutSecretValueRequest { SecretId = nombre, SecretString = valor });
            }
            catch (ResourceNotFoundException)
            {
                await _cliente.CreateSecretAsync(new CreateSecretRequest
                {
                    Name = nombre,
                    SecretString = valor,
                    Description = "API key de IA del portal Consenso (complementos IA)"
                });
            }
            catch (InvalidRequestException)
            {
                // Secreto programado para borrado: se restaura y se reemplaza
                await _cliente.RestoreSecretAsync(new RestoreSecretRequest { SecretId = nombre });
                await _cliente.PutSecretValueAsync(new PutSecretValueRequest { SecretId = nombre, SecretString = valor });
            }
        }

        public async Task<string> LeerAsync(string nombre)
        {
            try
            {
                var respuesta = await _cliente.GetSecretValueAsync(new GetSecretValueRequest { SecretId = nombre });
                return respuesta.SecretString;
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
        }

        public async Task EliminarAsync(string nombre)
        {
            try
            {
                await _cliente.DeleteSecretAsync(new DeleteSecretRequest { SecretId = nombre, RecoveryWindowInDays = 7 });
            }
            catch (ResourceNotFoundException)
            {
            }
        }
    }
}
