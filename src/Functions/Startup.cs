using System;
using Functions;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Functions;

public class Startup : FunctionsStartup
{
    private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .SetBasePath(Environment.CurrentDirectory)
        .AddJsonFile("appsettings.json", true)
        .AddEnvironmentVariables()
        .Build();

    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddSingleton(_ => new CosmosClientBuilder(Configuration["CosmosDBConnection"]).Build());
    }
}
