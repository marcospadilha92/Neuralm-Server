﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Neuralm.Application.Interfaces;
using Neuralm.Mapping;
using Neuralm.Persistence.Contexts;

namespace Neuralm.Presentation.CLI
{
    internal class Startup
    {
        private IGenericServiceProvider _serviceProvider;

        internal Task InitializeAsync(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _serviceProvider = new ServiceCollection()
                .AddConfigurations(configuration)
                .AddApplicationServices()
                .AddJwtBearerBasedAuthentication()
                .BuildServiceProvider()
                .ToGenericServiceProvider();

            List<Task> tasks = new List<Task>
            {
                Task.Run(VerifyDatabaseConnection),
                Task.Run(MapMessagesToServices)
            };
            return Task.WhenAll(tasks);
        }
        internal IGenericServiceProvider GetServiceProvider()
        {
            return _serviceProvider ?? throw new InitializationException("GenericServiceProvider is unset. Call InitializeAsync() first.");
        }
        private Task VerifyDatabaseConnection()
        {
            Console.WriteLine("Starting database verification..");
            NeuralmDbContext neuralmDbContext = _serviceProvider.GetService<IFactory<NeuralmDbContext>>().Create();
            RelationalDatabaseCreator relationalDatabaseCreator = neuralmDbContext.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
            bool? exists = relationalDatabaseCreator?.Exists();
            Console.WriteLine($"Database {(exists.Value ? "" : "does not ")}exists.");
            if (!exists.Value)
            {
                relationalDatabaseCreator?.Create();
                Console.WriteLine("Database created.");
            }

            IEnumerable<string> pendingMigrations = neuralmDbContext.Database.GetPendingMigrations().ToList();
            bool hasPendingMigrations = pendingMigrations.Any();
            Console.WriteLine($"Database {(hasPendingMigrations ? "has" : "does not have")} pending migrations.");
            if (hasPendingMigrations)
            {
                foreach (string pendingMigration in pendingMigrations)
                {
                    Console.WriteLine($"\tPending migration: {pendingMigration}");
                }
                neuralmDbContext.Database.Migrate();
                Console.WriteLine("Applied the pending migrations to the database.");
            }
            Console.WriteLine("Finished database verification!");
            return Task.CompletedTask;
        }
        private Task MapMessagesToServices()
        {
            // NOTE: Create the singleton once to map the services.
            _ = _serviceProvider.GetService<MessageToServiceMapper>();
            return Task.CompletedTask;
        }
    }
}
