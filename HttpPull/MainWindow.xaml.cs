using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GutenbergApp
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = CreateClient();

        private const string ApiBase = "https://gutendex.com/books";

        public MainWindow()
        {
            InitializeComponent();
        }

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("GutenbergApp/1.0 (student project)");
            return c;
        }

        private async void Hamlet_Click(object sender, RoutedEventArgs e)
        {
            const string url = "https://www.gutenberg.org/cache/epub/1524/pg1524.txt";

            SetBusy(true, "Завантаження «Гамлета»...");
            imgCover.Source = null;
            try
            {
                txtContent.Text = await client.GetStringAsync(url);
                txtContent.ScrollToHome();
                SetStatus("«Гамлет» завантажено.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Top_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true, "Завантаження списку популярних книжок...");
            try
            {
                var books = await LoadBooksAsync($"{ApiBase}?sort=popular", maxCount: 100);
                lstBooks.ItemsSource = books;
                SetStatus($"Знайдено {books.Count} популярних книжок. Натисніть на назву, щоб відкрити текст.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string query = txtSearch.Text.Trim();
            if (query.Length == 0)
            {
                MessageBox.Show("Введіть текст для пошуку.");
                return;
            }

            SetBusy(true, $"Пошук «{query}»...");
            try
            {
                string url = $"{ApiBase}?search={Uri.EscapeDataString(query)}";
                var books = await LoadBooksAsync(url, maxCount: 100);
                lstBooks.ItemsSource = books;
                SetStatus(books.Count == 0
                    ? "Нічого не знайдено."
                    : $"Знайдено книжок: {books.Count}.");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Search_Click(sender, e);
        }

        private async void Author_Click(object sender, RoutedEventArgs e)
        {
            string lastName = txtLastName.Text.Trim();
            string firstName = txtFirstName.Text.Trim();

            if (lastName.Length == 0)
            {
                MessageBox.Show("Введіть хоча б прізвище автора.");
                return;
            }

            SetBusy(true, $"Пошук книжок автора {lastName} {firstName}...");
            try
            {
                string query = $"{lastName} {firstName}".Trim();
                string url = $"{ApiBase}?search={Uri.EscapeDataString(query)}";

                var found = await LoadBooksAsync(url, maxCount: int.MaxValue);

                var books = found.Where(b => b.Authors.Any(a =>
                        a.Name != null &&
                        a.Name.Contains(lastName, StringComparison.OrdinalIgnoreCase) &&
                        (firstName.Length == 0 ||
                         a.Name.Contains(firstName, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                lstBooks.ItemsSource = books;

                if (books.Count == 0)
                {
                    SetStatus("Книжок цього автора не знайдено.");
                    return;
                }

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "GutenbergBooks",
                    MakeSafeFileName($"{lastName} {firstName}".Trim()));
                Directory.CreateDirectory(folder);

                int saved = 0;
                for (int i = 0; i < books.Count; i++)
                {
                    var book = books[i];
                    SetStatus($"Завантаження {i + 1} з {books.Count}: {book.Title}");

                    if (book.TextUrl == null)
                        continue;

                    try
                    {
                        string text = await client.GetStringAsync(book.TextUrl);
                        string fileName = MakeSafeFileName($"{book.Id} - {book.Title}") + ".txt";
                        await File.WriteAllTextAsync(Path.Combine(folder, fileName), text);
                        saved++;
                    }
                    catch
                    {
                    }
                }

                SetStatus($"Збережено {saved} з {books.Count} книжок у папку: {folder}");
                MessageBox.Show($"Збережено {saved} книжок у папку:\n{folder}", "Готово");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }
        private async void LstBooks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstBooks.SelectedItem is not Book book)
                return;

            imgCover.Source = book.CoverUrl != null
                ? new BitmapImage(new Uri(book.CoverUrl))
                : null;

            if (book.TextUrl == null)
            {
                txtContent.Text = "Для цієї книжки немає текстової версії.";
                return;
            }

            SetStatus($"Завантаження тексту: {book.Title}...");
            txtContent.Text = "Завантаження...";
            try
            {
                txtContent.Text = await client.GetStringAsync(book.TextUrl);
                txtContent.ScrollToHome();
                SetStatus($"Відкрито: {book.Title}");
            }
            catch (Exception ex)
            {
                txtContent.Text = "";
                ShowError(ex);
            }
        }
        private async Task<List<Book>> LoadBooksAsync(string url, int maxCount)
        {
            var result = new List<Book>();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            while (url != null && result.Count < maxCount)
            {
                string json = await client.GetStringAsync(url);
                var page = JsonSerializer.Deserialize<GutenbergResponse>(json, options);
                if (page?.Results == null)
                    break;

                result.AddRange(page.Results);
                url = page.Next;
            }

            return result.Take(maxCount).ToList();
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 120 ? name.Substring(0, 120) : name;
        }

        private void SetBusy(bool busy, string status = null)
        {
            btnHamlet.IsEnabled = btnTop.IsEnabled = btnSearch.IsEnabled = btnAuthor.IsEnabled = !busy;
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;
            if (status != null)
                SetStatus(status);
        }

        private void SetStatus(string text) => txtStatus.Text = text;

        private void ShowError(Exception ex)
        {
            SetStatus("Помилка.");
            MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
