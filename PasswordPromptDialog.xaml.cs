using System.Windows;

namespace MovieApp
{
    public partial class PasswordPromptDialog : Window
    {
        /// <summary>
        /// Пароль, введённый пользователем. Доступен после DialogResult == true.
        /// </summary>
        public string EnteredPassword { get; private set; } = string.Empty;

        public PasswordPromptDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => PwdBox.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PwdBox.Password;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
