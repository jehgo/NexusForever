using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Auth.Model;
using NexusForever.Database.Configuration;
using NLog;

namespace NexusForever.Database.Auth
{
    public class AuthDatabase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private readonly IDatabaseConfiguration config;

        public AuthDatabase(IDatabaseConfiguration config)
        {
            this.config = config;
        }

        public void Migrate()
        {
            using (var context = new AuthContext(config))
            {
                List<string> migrations = context.Database.GetPendingMigrations().ToList();
                if (migrations.Count > 0)
                {
                    log.Info($"Applying {migrations.Count} authentication database migration(s)...");
                    foreach (string migration in migrations)
                        log.Info(migration);

                    context.Database.Migrate();
                }
            }
        }

        public async Task Save(Action<AuthContext> action)
        {
            using (var context = new AuthContext(config))
            {
                action.Invoke(context);
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Selects an <see cref="AccountModel"/> asynchronously that matches the supplied email.
        /// </summary>
        public async Task<AccountModel> GetAccountAsync(string email)
        {
            using (var context = new AuthContext(config))
                return await context.Account.SingleOrDefaultAsync(a => a.Email == email);
        }

        /// <summary>
        /// Selects an <see cref="AccountModel"/> asynchronously that matches the supplied email and game token.
        /// </summary>
        public async Task<AccountModel> GetAccountAsync(string email, string gameToken)
        {
            using (var context = new AuthContext(config))
                return await context.Account.SingleOrDefaultAsync(a => a.Email == email && a.GameToken == gameToken);
        }

        /// <summary>
        /// Selects an <see cref="AccountModel"/> asynchronously that matches the supplied email and session key.
        /// </summary>
        public async Task<AccountModel> GetAccountAsync(string email, byte[] sessionKeyBytes)
        {
            string sessionKey = BitConverter.ToString(sessionKeyBytes).Replace("-", "");
            using (var context = new AuthContext(config))
                return await context.Account
                    .Include(a => a.CostumeUnlocks)
                    .Include(a => a.Currencies)
                    .Include(a => a.GenericUnlocks)
                    .Include(a => a.Keybindings)
                    .Include(a => a.Entitlements)
                    .SingleOrDefaultAsync(a => a.Email == email && a.SessionKey == sessionKey);
        }

        /// <summary>
        /// Create a new account with the supplied email, salt and SRP6 password verifier.
        /// </summary>
        public void CreateAccount(string email, string salt, string verifier)
        {
            using (var context = new AuthContext(config))
            {
                context.Account.Add(new AccountModel
                {
                    Email = email,
                    S     = salt,
                    V     = verifier
                });

                context.SaveChanges();
            }
        }

        /// <summary>
        /// Delete an existing account with the supplied email.
        /// </summary>
        public bool DeleteAccount(string email)
        {
            // Thanks Rawaho!
            using (var context = new AuthContext(config))
            {
                AccountModel account = context.Account.SingleOrDefault(a => a.Email == email);
                if (account == null)
                    return false;

                context.Account.Remove(account);
                return context.SaveChanges() > 0;
            }
        }

        /// <summary>
        /// Update <see cref="AccountModel"/> with supplied game token asynchronously.
        /// </summary>
        public async Task UpdateAccountGameToken(AccountModel account, string gameToken)
        {
            account.GameToken = gameToken;
            using (var context = new AuthContext(config))
            {
                EntityEntry<AccountModel> entity = context.Attach(account);
                entity.Property(p => p.GameToken).IsModified = true;
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Update <see cref="AccountModel"/> with supplied session key asynchronously.
        /// </summary>
        public async Task UpdateAccountSessionKey(AccountModel account, byte[] sessionKeyBytes)
        {
            account.SessionKey = BitConverter.ToString(sessionKeyBytes).Replace("-", "");
            using (var context = new AuthContext(config))
            {
                EntityEntry<AccountModel> entity = context.Attach(account);
                entity.Property(p => p.SessionKey).IsModified = true;
                await context.SaveChangesAsync();
            }
        }

        public ImmutableList<ServerModel> GetServers()
        {
            using (var context = new AuthContext(config))
                return context.Server
                    .AsNoTracking()
                    .ToImmutableList();
        }

        public ImmutableList<ServerMessageModel> GetServerMessages()
        {
            using (var context = new AuthContext(config))
                return context.ServerMessage
                    .AsNoTracking()
                    .ToImmutableList();
        }
    }
}