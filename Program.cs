using Amazon.Lambda.AspNetCoreServer.Hosting;

namespace esupplier
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices(services =>
                {
                    // Add AWS Lambda hosting support
                    services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
                });
    }
}
