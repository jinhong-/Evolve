﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCoreSample
{
    public class Startup
    {
        private readonly ILogger _logger;

        public Startup(IConfiguration configuration, IHostingEnvironment env, ILogger<Startup> logger)
        {
            Configuration = configuration;
            Env = env;
            _logger = logger;

            MigrateDatabase(); // Evolve
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Env { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("Hello World!");
            });
        }

        private void MigrateDatabase()
        {
            string location = Env.IsProduction() || Env.IsStaging() // exclude db/datasets from production environment
                ? "db/migrations"
                : "db";

            try
            {
                var cnx = new SqliteConnection(Configuration.GetConnectionString("MyDatabase"));
                var evolve = new Evolve.Evolve(cnx, msg => _logger.LogInformation(msg))
                {
                    Locations = new[] { location },
                    IsEraseDisabled = true,
                    Placeholders = new Dictionary<string, string>
                    {
                        ["${table4}"] = "table4"
                    }
                };

                evolve.Migrate();
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Database migration failed.", ex);
                throw;
            }
        }
    }
}
