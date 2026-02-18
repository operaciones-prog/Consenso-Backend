using Amazon.Lambda.AspNetCoreServer;

namespace esupplier
{
    /// <summary>
    /// Lambda entry point for API Gateway HTTP API
    /// This class extends from APIGatewayHttpApiV2ProxyFunction which contains the method 
    /// FunctionHandlerAsync which is the actual Lambda function handler.
    /// </summary>
    public class LambdaEntryPoint : APIGatewayHttpApiV2ProxyFunction
    {
        /// <summary>
        /// The builder has configuration, logging and Amazon API Gateway already configured.
        /// The startup class is used to configure ASP.NET Core middleware, services and other hosting configuration.
        /// </summary>
        /// <param name="builder"></param>
        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();
        }
    }
}

