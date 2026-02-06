using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PautaDinamicaApp.Models;
using PautaDinamicaApp.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace PautaDinamicaApp.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly SessionService _sessionService;
        private ObservableCollection<UserModel> _users = new();
        private UserModel? _selectedUser;
        private string _password = "";
        private string _adminPassword = "";
        private string _newUsername = "";
        private string _newPassword = "";
        private string _resetNewPassword = "";

        private bool _isManageMode;
        private bool _isMasterResetMode;
        private bool _hasUsers;

        public ObservableCollection<UserModel> Users { get => _users; set => SetProperty(ref _users, value); }
        public UserModel? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    IsMasterResetMode = false;
                }
            }
        }

        public string Password { get => _password; set => SetProperty(ref _password, value); }
        public string AdminPassword { get => _adminPassword; set => SetProperty(ref _adminPassword, value); }
        public string NewUsername { get => _newUsername; set => SetProperty(ref _newUsername, value); }
        public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
        public string ResetNewPassword { get => _resetNewPassword; set => SetProperty(ref _resetNewPassword, value); }

        public bool IsManageMode { get => _isManageMode; set => SetProperty(ref _isManageMode, value); }
        public bool IsMasterResetMode { get => _isMasterResetMode; set => SetProperty(ref _isMasterResetMode, value); }
        public bool HasUsers { get => _hasUsers; set => SetProperty(ref _hasUsers, value); }

        public ICommand LoginCommand { get; }
        public ICommand ToggleManageModeCommand { get; }
        public ICommand CreateUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ToggleMasterResetCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand ToggleThemeCommand { get; }


        public event Action? OnLoginSuccess;
        public event Action? RequestClearPasswords;

        public LoginViewModel()
        {
            _sessionService = new SessionService();
            LoadUsers();

            // Aplicar tema guardado al iniciar
            var defaultSettings = new StorageService().LoadSettings();
            new ThemeService().SetTheme(defaultSettings.Theme);

            LoginCommand = new RelayCommand(_ => Login());
            ToggleManageModeCommand = new RelayCommand(_ => ToggleManageMode());
            CreateUserCommand = new RelayCommand(_ => CreateUser());
            DeleteUserCommand = new RelayCommand(u => DeleteUser(u as UserModel));
            ToggleMasterResetCommand = new RelayCommand(_ => ToggleMasterReset());
            ResetPasswordCommand = new RelayCommand(_ => ResetPassword());
            ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        }

        private void ToggleTheme()
        {
            var storage = new StorageService();
            var settings = storage.LoadSettings();
            settings.Theme = settings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            storage.SaveSettings(settings);
            new ThemeService().SetTheme(settings.Theme);
        }


        private void ToggleManageMode()
        {
            IsManageMode = !IsManageMode;
            ClearSensitiveData();
        }

        private void ToggleMasterReset()
        {
            IsMasterResetMode = !IsMasterResetMode;
            ClearSensitiveData();
        }

        private void ClearSensitiveData()
        {
            Password = "";
            AdminPassword = "";
            NewPassword = "";
            ResetNewPassword = "";
            RequestClearPasswords?.Invoke();
        }

        private void LoadUsers()
        {
            var usersList = _sessionService.LoadUsers();
            Users = new ObservableCollection<UserModel>(usersList);
            HasUsers = usersList.Any();

            // We NO LONGER force CreateMode here. 
            // The UI will show Empty State if !HasUsers.
            if (SelectedUser == null)
            {
                SelectedUser = Users.FirstOrDefault();
            }
        }

        private void Login()
        {
            if (SelectedUser == null) return;

            if (_sessionService.Login(SelectedUser.Username, Password))
            {
                // Sincronizar tema: Copiar el tema de la pantalla de login (default) al perfil del usuario
                var storageDefault = new StorageService("default");
                var currentTheme = storageDefault.LoadSettings().Theme;

                var storageUser = new StorageService(SelectedUser.Username);
                var userSettings = storageUser.LoadSettings();
                userSettings.Theme = currentTheme;
                storageUser.SaveSettings(userSettings);

                OnLoginSuccess?.Invoke();
            }
            else
            {
                ClearSensitiveData();
                MessageBox.Show("Contraseña incorrecta.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateUser()
        {
            if (string.IsNullOrWhiteSpace(NewUsername))
            {
                MessageBox.Show("Ingrese un nombre de usuario.");
                return;
            }

            try
            {
                _sessionService.RegisterUser(NewUsername, NewPassword);
                LoadUsers();
                SelectedUser = Users.FirstOrDefault(u => u.Username == NewUsername);
                IsManageMode = false; // Go back to login
                NewUsername = "";
                ClearSensitiveData();
                MessageBox.Show($"Usuario '{SelectedUser?.Username}' creado con éxito.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void DeleteUser(UserModel? user)
        {
            if (user == null) return;

            bool isAuthorized = false;

            if (_sessionService.IsMasterPassword(AdminPassword))
            {
                isAuthorized = true;
            }
            else if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                if (!string.IsNullOrEmpty(Password) && SessionService.HashPassword(Password) == user.PasswordHash)
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                ClearSensitiveData();
                MessageBox.Show("Autorización incorrecta para eliminar el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            var res = MessageBox.Show($"¿Realmente desea eliminar al usuario '{user.Username}'?\nEsta acción respaldará sus datos pero borrará el perfil.",
                "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                _sessionService.DeleteUser(user.Username);
                ClearSensitiveData();
                LoadUsers();
                MessageBox.Show($"Usuario '{user.Username}' eliminado.");
            }
        }

        private void ResetPassword()
        {
            if (SelectedUser == null) return;

            if (string.IsNullOrWhiteSpace(ResetNewPassword))
            {
                ClearSensitiveData();
                MessageBox.Show("Por favor, ingrese la nueva contraseña que desea establecer.", "Dato Faltante");
                return;
            }

            if (_sessionService.IsMasterPassword(AdminPassword))
            {
                var users = _sessionService.LoadUsers();
                var u = users.FirstOrDefault(user => user.Username == SelectedUser.Username);
                if (u != null)
                {
                    u.PasswordHash = SessionService.HashPassword(ResetNewPassword);
                    _sessionService.SaveUsers(users);
                    MessageBox.Show("Contraseña actualizada con éxito.", "Éxito");
                    IsMasterResetMode = false;
                    ClearSensitiveData();
                    LoadUsers();
                }
            }
            else
            {
                ClearSensitiveData();
                MessageBox.Show("Clave de Autorización Incorrecta.", "Error");
            }
        }
    }
}
