﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Jaya.Provider.GoogleDrive.Models;
using Jaya.Provider.GoogleDrive.Views;
using Jaya.Shared.Base;
using Jaya.Shared.Models;
using Jaya.Shared.Services;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Jaya.Provider.GoogleDrive.Services
{
    [Export(typeof(IProviderService))]
    [Shared]
    public class GoogleDriveService : ProviderServiceBase, IProviderService
    {
        const string CLIENT_ID = "538742722606-equtrav33c2tqaq2io7h19mkf4ch6jbp.apps.googleusercontent.com";
        const string CLIENT_SECRET = "UGprjYfFkb--RHnGbgAnm_Aj";

        const string MIME_TYPE_FILE = "application/vnd.google-apps.file";
        const string MIME_TYPE_DIRECTORY = "application/vnd.google-apps.folder";

        /// <summary>
        /// Refer pages https://www.daimto.com/google-drive-authentication-c/ and https://www.daimto.com/google-drive-api-c/ for examples.
        /// </summary>
        public GoogleDriveService()
        {
            Name = "Google Drive";
            ImagePath = "avares://Jaya.Provider.GoogleDrive/Assets/Images/GoogleDrive-32.png";
            Description = "View your Google Drive accounts, inspect their contents and play with directories & files stored within them.";
            IsRootDrive = true;
            ConfigurationEditorType = typeof(ConfigurationView);
        }

        BaseClientService.Initializer GetServiceInitializer(UserCredential credentials)
        {
            return new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials
            };
        }

        async Task<UserCredential> GetCredentials()
        {
            var scopes = new string[]
            {
                DriveService.Scope.Drive,
                Oauth2Service.Scope.UserinfoProfile,
                Oauth2Service.Scope.UserinfoEmail
            };
            var secret = new ClientSecrets
            {
                ClientId = CLIENT_ID,
                ClientSecret = CLIENT_SECRET
            };
            var dataStore = new FileDataStore(ConfigurationDirectory, true);

            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(secret, scopes, Environment.UserName, CancellationToken.None, dataStore);
            if (credentials.Token.IsExpired(SystemClock.Default))
            {
                var isRefreshed = await credentials.RefreshTokenAsync(CancellationToken.None);
                if (!isRefreshed)
                    return null;
            }

            return credentials;
        }

        public override async Task<DirectoryModel> GetDirectoryAsync(AccountModelBase account, string path = null)
        {
            if (path == null)
                path = string.Empty;

            var model = GetFromCache(account, path);
            if (model != null)
                return model;
            else
                model = new DirectoryModel();

            model.Name = path;
            model.Path = path;
            model.Directories = new List<DirectoryModel>();
            model.Files = new List<FileModel>();

            var credentials = await GetCredentials();

            FileList entries;
            using (var client = new DriveService(GetServiceInitializer(credentials)))
            {
                entries = await client.Files.List().ExecuteAsync();
            }
            foreach (var entry in entries.Files)
            {
                if (entry.MimeType.Equals(MIME_TYPE_DIRECTORY))
                {
                    var directory = new DirectoryModel();
                    directory.Id = entry.Id;
                    directory.Name = entry.Name;
                    directory.Path = entry.Name;
                    model.Directories.Add(directory);

                }
                else if (entry.MimeType.Equals(MIME_TYPE_FILE))
                {
                    var file = new FileModel();
                    file.Id = entry.Id;
                    file.Name = entry.Name;
                    file.Path = entry.Name;
                    model.Files.Add(file);
                }
            }

            AddToCache(account, model);
            return model;
        }

        protected override async Task<AccountModelBase> AddAccountAsync()
        {
            var credentials = await GetCredentials();
            if (credentials == null)
                return null;

            Userinfoplus userInfo;
            using (var authService = new Oauth2Service(GetServiceInitializer(credentials)))
            {
                userInfo = await authService.Userinfo.Get().ExecuteAsync();
            }

            var config = GetConfiguration<ConfigModel>();

            var provider = new AccountModel(userInfo.Id, userInfo.Name)
            {
                Email = userInfo.Email
            };

            config.Accounts.Add(provider);
            SetConfiguration(config);

            return provider;
        }

        protected override async Task<bool> RemoveAccountAsync(AccountModelBase account)
        {
            var config = GetConfiguration<ConfigModel>();

            var isRemoved = config.Accounts.Remove(account as AccountModel);
            if (isRemoved)
                SetConfiguration(config);

            return await Task.Run(() => isRemoved);
        }

        public override async Task<IEnumerable<AccountModelBase>> GetAccountsAsync()
        {
            var config = GetConfiguration<ConfigModel>();
            return await Task.Run(() => config.Accounts);
        }
    }
}
