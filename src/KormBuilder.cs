﻿using Kros.Data;
using Kros.KORM.Extensions.Asp.Properties;
using Kros.KORM.Migrations;
using Kros.KORM.Migrations.Middleware;
using Kros.KORM.Migrations.Providers;
using Kros.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Kros.KORM.Extensions.Asp
{
    /// <summary>
    /// Builder for initialization of <see cref="IDatabase"/>.
    /// </summary>
    public class KormBuilder
    {
        /// <summary>
        /// Default connection string name in configuration if no name is provided.
        /// </summary>
        public const string DefaultConnectionStringName = "DefaultConnection";

        private readonly IDatabaseBuilder _builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="KormBuilder"/> class.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">The database connection string.</param>
        public KormBuilder(IServiceCollection services, string connectionString)
            : this(services, new KormConnectionSettings(connectionString))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KormBuilder"/> class.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionSettings">The database connection settings.</param>
        public KormBuilder(IServiceCollection services, KormConnectionSettings connectionSettings)
        {
            Services = Check.NotNull(services, nameof(services));
            ConnectionSettings = Check.NotNull(connectionSettings, nameof(connectionSettings));
            Check.NotNullOrWhiteSpace(
                connectionSettings.ConnectionString, nameof(connectionSettings), Resources.EmptyConnectionStringInSettings);

            _builder = Database.Builder;
            _builder.UseConnection(connectionSettings);
        }

        /// <summary>
        /// Gets the service collection.
        /// </summary>
        public IServiceCollection Services { get; }

        internal KormConnectionSettings ConnectionSettings { get; }

        /// <summary>
        /// Use database configuration.
        /// </summary>
        /// <typeparam name="TConfiguration">Configuration type.</typeparam>
        /// <returns>KORM builder.</returns>
        public KormBuilder UseDatabaseConfiguration<TConfiguration>() where TConfiguration : DatabaseConfigurationBase, new()
        {
            _builder.UseDatabaseConfiguration<TConfiguration>();
            return this;
        }

        /// <summary>
        /// Use database configuration.
        /// </summary>
        /// <param name="databaseConfiguration">Instance of database configuration.</param>
        /// <returns>KORM builder.</returns>
        public KormBuilder UseDatabaseConfiguration(DatabaseConfigurationBase databaseConfiguration)
        {
            _builder.UseDatabaseConfiguration(databaseConfiguration);
            return this;
        }

        /// <summary>
        /// Initializes database for using Id generator.
        /// </summary>
        /// <returns>This instance.</returns>
        public KormBuilder InitDatabaseForIdGenerator()
        {
            IIdGeneratorFactory factory = IdGeneratorFactories.GetFactory(
                ConnectionSettings.ConnectionString, ConnectionSettings.KormProvider);
            using (IIdGenerator idGenerator = factory.GetGenerator(string.Empty))
            {
                idGenerator.InitDatabaseForIdGenerator();
            }
            return this;
        }

        /// <summary>
        /// Adds configuration for <see cref="MigrationsMiddleware"/> into <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="setupAction">Setup migration options.</param>
        /// <returns>This instance of <see cref="KormBuilder"/>.</returns>
        public KormBuilder AddKormMigrations(Action<MigrationOptions> setupAction = null)
        {
            Services
                .AddMemoryCache()
                .AddTransient((Func<IServiceProvider, IMigrationsRunner>)(s =>
                {
                    var database = new Database(ConnectionSettings);
                    MigrationOptions options = SetupMigrationOptions(setupAction);
                    return new MigrationsRunner(database, options);
                }));

            return this;
        }

        private static MigrationOptions SetupMigrationOptions(Action<MigrationOptions> setupAction)
        {
            var options = new MigrationOptions();

            if (setupAction != null)
            {
                setupAction.Invoke(options);
            }
            else
            {
                options.AddScriptsProvider(AssemblyMigrationScriptsProvider.GetEntryAssemblyProvider());
            }

            return options;
        }

        /// <summary>
        /// Execute database migration. Migrations are executed only if they were enabled in constructor.
        /// </summary>
        public void Migrate()
        {
            if (ConnectionSettings.AutoMigrate)
            {
                Services.BuildServiceProvider()
                    .GetService<IMigrationsRunner>()
                    .MigrateAsync()
                    .Wait();
            }
        }

        internal IDatabase Build() => _builder.Build();
    }
}
