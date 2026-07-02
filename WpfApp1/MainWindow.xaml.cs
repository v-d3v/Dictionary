using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Dictionary
{
    public partial class MainWindow : Window
    {
        // HttpClient — инструмент для HTTP-запросов в интернет
        // static readonly = создаётся один раз за всё время работы программы
        private static readonly HttpClient _http = new HttpClient();

        // ссылка на MP3 с произношением. null = ещё не получена
        private string _audioUrl = null;

        // список последних поисковых запросов (максимум 5)
        private readonly List<string> _history = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            // заголовок запроса — представляем наше приложение серверу
            // без него некоторые API могут отклонить запрос
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        // нажата кнопка "Найти"
        private void SearchButton_Click(object sender, RoutedEventArgs e) =>
            _ = SearchWordAsync();

        // нажата клавиша в поле ввода — запускаем поиск только если это Enter
        private void WordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _ = SearchWordAsync();
        }

        // двойной клик по слову в истории — вставляем его в поле и ищем
        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedItem is string word)
            {
                WordInput.Text = word;
                _ = SearchWordAsync();
            }
        }

        // нажата кнопка "Очистить" историю
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            HistoryList.ItemsSource = null;
        }

        // основной метод поиска
        // async/await — ждём ответа от сервера не замораживая окно
        private async Task SearchWordAsync()
        {
            string word = WordInput.Text.Trim();

            if (string.IsNullOrEmpty(word))
            {
                StatusBar.Text = "Введите слово для поиска.";
                return;
            }

            if (word.Length > 50)
            {
                StatusBar.Text = "Слишком длинный запрос (максимум 50 символов).";
                return;
            }

            // готовим интерфейс к новому поиску
            StatusBar.Text = "Поиск...";
            ResultPanel.Children.Clear();
            PlayButton.IsEnabled = false;
            _audioUrl = null;

            try
            {
                // формируем адрес запроса
                string url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{Uri.EscapeDataString(word)}";

                // отправляем запрос, получаем JSON-строку в ответ
                string json = await _http.GetStringAsync(url);

                // превращаем JSON-строку в объекты C#
                dynamic data = new JavaScriptSerializer().DeserializeObject(json);
                var entries = (object[])data;
                var entry = (Dictionary<string, object>)entries[0];

                // достаём слово и транскрипцию
                string headword = entry.ContainsKey("word")
                    ? entry["word"].ToString() : word;
                string phonetic = entry.ContainsKey("phonetic")
                    ? entry["phonetic"].ToString() : "";

                // ищем ссылку на MP3 с произношением
                if (entry.ContainsKey("phonetics"))
                {
                    foreach (var ph in (object[])entry["phonetics"])
                    {
                        var phDict = (Dictionary<string, object>)ph;
                        if (phDict.ContainsKey("audio") &&
                            !string.IsNullOrEmpty(phDict["audio"].ToString()))
                        {
                            _audioUrl = phDict["audio"].ToString();
                            if (!_audioUrl.StartsWith("http"))
                                _audioUrl = "https:" + _audioUrl;
                            break;
                        }
                    }
                }

                // выводим заголовок
                AddHeader($"{headword}   {phonetic}");

                // обходим все значения слова (noun, verb, adjective и т.д.)
                if (entry.ContainsKey("meanings"))
                {
                    int meaningNum = 1;
                    foreach (var m in (object[])entry["meanings"])
                    {
                        var mDict = (Dictionary<string, object>)m;
                        string partOfSpeech = mDict.ContainsKey("partOfSpeech")
                            ? mDict["partOfSpeech"].ToString() : "";

                        AddSubHeader($"{meaningNum}. {partOfSpeech}");

                        if (mDict.ContainsKey("definitions"))
                        {
                            int defNum = 1;
                            foreach (var d in (object[])mDict["definitions"])
                            {
                                var dDict = (Dictionary<string, object>)d;

                                string def = dDict.ContainsKey("definition")
                                    ? dDict["definition"].ToString() : "";
                                string example = dDict.ContainsKey("example")
                                    ? dDict["example"].ToString() : "";

                                AddDefinition($"   {defNum}. {def}");

                                if (!string.IsNullOrEmpty(example))
                                    AddExample($"      Пример: \"{example}\"");

                                defNum++;
                            }
                        }
                        meaningNum++;
                    }
                }

                // включаем кнопку звука если нашли ссылку
                PlayButton.IsEnabled = _audioUrl != null;
                StatusBar.Text = $"Найдено: \"{headword}\"";

                // обновляем историю — без дубликатов, максимум 5 слов
                if (!_history.Contains(headword))
                {
                    _history.Insert(0, headword);
                    if (_history.Count > 5)
                        _history.RemoveAt(5);
                }
                HistoryList.ItemsSource = null;
                HistoryList.ItemsSource = _history;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                // 404 = слово не найдено в словаре, это не баг программы
                AddError($"Слово \"{word}\" не найдено. Проверьте написание.");
                StatusBar.Text = "Не найдено.";
            }
            catch (Exception ex)
            {
                // любая другая ошибка: нет интернета, сервер недоступен и т.д.
                AddError($"Ошибка: {ex.Message}");
                StatusBar.Text = "Произошла ошибка.";
            }
        }

        // открывает произношение в браузере
        private void PlayAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioUrl != null)
                System.Diagnostics.Process.Start(_audioUrl);
        }

        // вспомогательные методы

        private void AddHeader(string text)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        private void AddSubHeader(string text)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Margin = new Thickness(0, 10, 0, 4)
            });
        }

        private void AddDefinition(string text)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 2, 0, 2)
            });
        }

        private void AddExample(string text)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        private void AddError(string text)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = Brushes.Red,
                TextWrapping = TextWrapping.Wrap
            });
        }
    }
}
