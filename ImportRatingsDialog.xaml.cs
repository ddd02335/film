using System.Windows;

namespace MovieApp
{
    public partial class ImportRatingsDialog : Window
    {
        public string SourceLogin => TxtLogin.Text.Trim();
        public string SourcePassword => TxtPassword.Password;

        public ImportRatingsDialog()
        {
            InitializeComponent();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SourceLogin) || string.IsNullOrWhiteSpace(SourcePassword))
            {
                MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
