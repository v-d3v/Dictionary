using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NAudio.Wave;

namespace Dictionary
{
    public partial class MainWindow : Window
    {
        // сервис для работы с API — вынесен в отдельный класс
        private DictionaryService _service = new DictionaryService();

        // ссылка на аудио произношения
        private string _audioUrl = null;

        // история поиска
        private List<string> _history = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // нажата кнопка "Найти"
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SearchWordAsync(WordInput.Text.Trim());
        }

        // нажат Enter в поле ввода
        private void WordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _ = SearchWordAsync(WordInput.Text.Trim());
        }

        // нажата кнопка "Случайное слово" — используем второй API
        private async void RandomButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBar.Text = "Получаем случайное слово...";
            try
            {
                string randomWord = await _service.GetRandomWordAsync();
                WordInput.Text = randomWord;
                await SearchWordAsync(randomWord);
            }
            catch (Exception ex)
            {
                StatusBar.Text = "Ошибка: " + ex.Message;
            }
        }

        // двойной клик по слову в истории — повторяем поиск
        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedItem is string word)
            {
                WordInput.Text = word;
                _ = SearchWordAsync(word);
            }
        }

        // очищаем историю
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            HistoryList.ItemsSource = null;
        }

        // главный метод — ищем слово и показываем результат
        private async Task SearchWordAsync(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                StatusBar.Text = "Введите слово для поиска.";
                return;
            }

            StatusBar.Text = "Поиск...";
            ResultPanel.Children.Clear();
            PlayButton.IsEnabled = false;
            _audioUrl = null;

            try
            {
                // обращаемся к сервису — он делает запрос к API
                WordResult result = await _service.GetWordAsync(word);

                // показываем слово и транскрипцию
                AddHeader(result.Word + "   " + result.Phonetic);

                // показываем все определения
                int num = 1;
                foreach (DefinitionItem def in result.Definitions)
                {
                    AddSubHeader(num + ". " + def.PartOfSpeech);
                    AddDefinition("    " + def.Definition);

                    if (!string.IsNullOrEmpty(def.Example))
                        AddExample("    Пример: \"" + def.Example + "\"");

                    num++;
                }

                // сохраняем ссылку на аудио если есть
                if (!string.IsNullOrEmpty(result.AudioUrl))
                {
                    _audioUrl = result.AudioUrl;
                    PlayButton.IsEnabled = true;
                }

                StatusBar.Text = "Найдено: " + result.Word;

                // добавляем в историю без дубликатов
                if (!_history.Contains(result.Word))
                {
                    _history.Insert(0, result.Word);
                    if (_history.Count > 5)
                        _history.RemoveAt(5);
                }

                HistoryList.ItemsSource = null;
                HistoryList.ItemsSource = _history;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                AddError("Слово \"" + word + "\" не найдено. Проверьте написание.");
                StatusBar.Text = "Не найдено.";
            }
            catch (Exception ex)
            {
                AddError("Ошибка: " + ex.Message);
                StatusBar.Text = "Произошла ошибка.";
            }
        }

        // воспроизводим произношение прямо в приложении через NAudio
        private async void PlayAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioUrl == null) return;

            StatusBar.Text = "Загрузка аудио...";
            PlayButton.IsEnabled = false;

            try
            {
                // скачиваем MP3 в память
                using (var http = new HttpClient())
                {
                    byte[] audioData = await http.GetByteArrayAsync(_audioUrl);

                    // воспроизводим через NAudio
                    using (var ms = new MemoryStream(audioData))
                    using (var reader = new Mp3FileReader(ms))
                    using (var output = new WaveOutEvent())
                    {
                        output.Init(reader);
                        output.Play();

                        // ждём пока закончится воспроизведение
                        while (output.PlaybackState == PlaybackState.Playing)
                            await Task.Delay(100);
                    }
                }

                StatusBar.Text = "Найдено: " + WordInput.Text;
            }
            catch (Exception ex)
            {
                StatusBar.Text = "Ошибка воспроизведения: " + ex.Message;
            }
            finally
            {
                PlayButton.IsEnabled = true;
            }
        }

        // методы для вывода текста разных стилей

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
