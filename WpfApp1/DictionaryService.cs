using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace Dictionary
{
    // этот класс отвечает за всю работу с интернетом
    class DictionaryService
    {
        private HttpClient _http = new HttpClient();

        public DictionaryService()
        {
            // представляемся серверу как браузер, иначе API может отклонить запрос
            _http.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            );
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            // если сервер не ответил за 10 секунд — отменяем запрос
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        // первый эндпойнт: получить определение слова
        // https://api.dictionaryapi.dev/api/v2/entries/en/{слово}
        public async Task<WordResult> GetWordAsync(string word)
        {
            string url = "https://api.dictionaryapi.dev/api/v2/entries/en/" + word;
            string json = await _http.GetStringAsync(url);

            // превращаем JSON-строку в объекты C#
            var serializer = new JavaScriptSerializer();
            var data = (object[])serializer.DeserializeObject(json);
            var entry = (Dictionary<string, object>)data[0];

            // заполняем наш объект с результатом
            var result = new WordResult();
            result.Word = entry["word"].ToString();

            // транскрипция — есть не у каждого слова, поэтому проверяем
            if (entry.ContainsKey("phonetic"))
                result.Phonetic = entry["phonetic"].ToString();

            // ищем ссылку на аудио произношения
            if (entry.ContainsKey("phonetics"))
            {
                var phonetics = (object[])entry["phonetics"];
                foreach (var p in phonetics)
                {
                    var ph = (Dictionary<string, object>)p;
                    if (ph.ContainsKey("audio") && ph["audio"].ToString() != "")
                    {
                        result.AudioUrl = ph["audio"].ToString();
                        if (!result.AudioUrl.StartsWith("http"))
                            result.AudioUrl = "https:" + result.AudioUrl;
                        break;
                    }
                }
            }

            // собираем все определения в один список
            if (entry.ContainsKey("meanings"))
            {
                var meanings = (object[])entry["meanings"];
                foreach (var m in meanings)
                {
                    var meaning = (Dictionary<string, object>)m;
                    string partOfSpeech = meaning["partOfSpeech"].ToString();

                    var definitions = (object[])meaning["definitions"];
                    foreach (var d in definitions)
                    {
                        var def = (Dictionary<string, object>)d;

                        var item = new DefinitionItem();
                        item.PartOfSpeech = partOfSpeech;
                        item.Definition = def["definition"].ToString();

                        if (def.ContainsKey("example"))
                            item.Example = def["example"].ToString();

                        result.Definitions.Add(item);
                    }
                }
            }

            return result;
        }

        // второй эндпойнт: получить случайное слово
        // https://random-word-api.herokuapp.com/word
        public async Task<string> GetRandomWordAsync()
        {
            string url = "https://random-word-api.herokuapp.com/word";
            string json = await _http.GetStringAsync(url);

            // API возвращает массив с одним словом, например: ["apple"]
            // убираем скобки и кавычки вручную
            string word = json.Trim('[', ']', '"');
            return word;
        }
    }

    // класс для хранения результата поиска слова
    class WordResult
    {
        public string Word { get; set; } = "";
        public string Phonetic { get; set; } = "";
        public string AudioUrl { get; set; } = "";

        // List — динамический список, сам растёт при добавлении элементов
        public List<DefinitionItem> Definitions { get; set; } = new List<DefinitionItem>();
    }

    // класс для одного определения
    class DefinitionItem
    {
        public string PartOfSpeech { get; set; } = "";
        public string Definition { get; set; } = "";
        public string Example { get; set; } = "";
    }
}
