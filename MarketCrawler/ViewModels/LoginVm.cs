using Neralem.Security;
using Neralem.Warframe.Core.DataAcquisition;
using Neralem.Wpf.Mvvm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using Neralem.Warframe.Core.DOMs;
using Neralem.Wpf.UI.Dialogs;
using Neralem.Wpf.Mvvm.Interfaces;

namespace MarketCrawler.ViewModels
{
    public class LoginVm : ViewModelBase
    {
        private ICommand tryLoginCommand;
        public ICommand TryLoginCommand
        {
            get
            {
                return tryLoginCommand ??= new RelayCommand(
                    async param =>
                    {
                        if (ApiProvider is null)
                            return;

                        if (await ApiProvider.TryLoginAsync(EmailAddress, Password) is User user)
                        {
                            User = user;

                            if (SaveUserData)
                                SaveLoginData();
                            (param as ICloseable)?.CloseIt(null);
                            OrderCollection myOrders = await ApiProvider.GetOwnOrdersAsync(MainVm.Items);
                            MainVm.PopupText = "LogIn erfolgreich";
                            MainVm.PopupVisible = true;
                        }
                        else
                        {
                            ExtMessageBox.Show("Fehler", "Fehler beim Login!", MessageBoxButton.OK, MessageBoxImage.Error, param as Window);
                        }
                    },
                    _ => (Password?.Length ?? 0) > 0 && !string.IsNullOrWhiteSpace(EmailAddress));
            }
        }

        private string emailAddress;
        public string EmailAddress
        {
            get => emailAddress;
            set 
            { 
                if (value != emailAddress)
                {
                    emailAddress = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private SecureString password;
        public SecureString Password
        {
            get => password;
            set 
            { 
                if (value != password)
                {
                    password = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool saveUserData;
        public bool SaveUserData
        {
            get => saveUserData;
            set 
            { 
                if (value != saveUserData)
                {
                    saveUserData = value;
                    NotifyPropertyChanged();

                    if (!SaveUserData && File.Exists(UserDataFilename))
                        File.Delete(UserDataFilename);
                }
            }
        }

        public User User { get; private set; }

        private void SaveLoginData()
        {
            dynamic data = new
            {
                email = EmailAddress,
                password = ProtectedData.Protect(Password.ToByteArray(), null, DataProtectionScope.LocalMachine)
            };

            File.WriteAllText(UserDataFilename, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private void LoadLoginData()
        {
            if (!File.Exists(UserDataFilename))
                return;

            JObject jObject = JObject.Parse(File.ReadAllText(UserDataFilename));
            EmailAddress = jObject["email"]?.ToObject<string>() ?? string.Empty;
            byte[] encryptedPassword = jObject["password"]?.ToObject<byte[]>();
            if (encryptedPassword is not null && encryptedPassword.Length > 0)
                Password = SecureStringExtensions.FromByteArray(ProtectedData.Unprotect(encryptedPassword, null, DataProtectionScope.LocalMachine));
        }

        private static string UserDataFilename => "LoginData.json";
        private MarketApiProvider ApiProvider { get; }
        public MainVm MainVm { get; }

        public LoginVm(MarketApiProvider apiProvider, MainVm mainVm)
        {
            ApiProvider = apiProvider;
            MainVm = mainVm;

            if (File.Exists(UserDataFilename))
            {
                SaveUserData = true;
                LoadLoginData();
            }
        }
    }
}