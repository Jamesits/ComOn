using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ComOn
{
    public class GitHubReleaseChecker: INotifyPropertyChanged
    {
        public enum CheckStatus
        {
            NotStarted,
            Checking,
            HaveUpdate,
            HaveNoUpdate,
            Failed,
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler UpdateCheckStatusChanged;

        public string User { get; }
        public string Project { get; }
        public CheckStatus UpdateCheckStatus { get; private set; }
        public Uri UpdatePageLinkUri { get; private set; }
        public Version UpdateVersion { get; private set; }
        public GitHubReleaseChecker(string user, string project)
        {
            User = user;
            Project = project;
            SetNewUpdateCheckStatus(CheckStatus.NotStarted);
        }

        private void SetNewUpdateCheckStatus(CheckStatus s)
        {
            UpdateCheckStatus = s;
            UpdateCheckStatusChanged?.Invoke(this, null);
        }

        public async Task RefreshReleaseInfo()
        {
            SetNewUpdateCheckStatus(CheckStatus.Checking);
            
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd($"{Project} ({Utils.GetVersion()}, +https://github.com/{User}/{Project})");
                    HttpResponseMessage response = await client
                        .GetAsync(new Uri($"https://api.github.com/repos/{User}/{Project}/releases/latest"))
                        .ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        // Parse the response body.
                        var ret = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var retDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(ret);
                        UpdatePageLinkUri = new Uri(retDictionary["html_url"] as string ?? throw new InvalidOperationException("Cannot cast \"html_url\" to string"));
                        UpdateVersion = ExtractVersion(retDictionary["tag_name"] as string ?? throw new InvalidOperationException("Cannot cast \"tag_name\" to string"));

                        Debug.WriteLine($"Latest version: {UpdateVersion}, Current version: {Utils.GetVersion()}");

                        SetNewUpdateCheckStatus(UpdateVersion != Utils.GetVersion() ? CheckStatus.HaveUpdate : CheckStatus.HaveNoUpdate);
                    }
                    else
                    {
                        SetNewUpdateCheckStatus(CheckStatus.Failed);
                        Debug.WriteLine($"HTTP client error {(int)response.StatusCode}: {response.ReasonPhrase}");
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                SetNewUpdateCheckStatus(CheckStatus.Failed);
                Debug.WriteLine($"Exception during update checking: {ex.Message}");
            }
            
        }

        public static Version ExtractVersion(string s)
        {
            int major = 0, minor = 0, revision = 0, build = 0;
            var numbers = Regex.Split(s, @"\D+");
            var index = 0;
            foreach (var value in numbers)
            {
                if (string.IsNullOrEmpty(value)) continue;
                var i = int.Parse(value, NumberStyles.None, new NumberFormatInfo());
                switch (index)
                {
                    case 0: 
                        major = i;
                        break;
                    case 1: 
                        minor = i;
                        break;
                    case 2: 
                        revision = i;
                        break;
                    case 3:
                        build = i;
                        break;
                }

                index += 1;
            }

            return new Version(major, minor, revision, build);
        }
    }
}
