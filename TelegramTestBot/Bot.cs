using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace TelegramTestBot
{
    class Bot: IDisposable
    {
        private string JsonFileName = "question.json";
        private string _idTelegramBot;
        private TelegramBotClient _client;
        private CancellationTokenSource _cts;
        private bool _isStarting = false;
        private QuestionContainer _containerQ;


        private Dictionary<long, ModelUserTest> _userDictionaryTesting = new Dictionary<long, ModelUserTest>();
        private readonly Dictionary<Commands, string> _dictCommands = new Dictionary<Commands, string>()
        {
            { Commands.Start, "/start" },
            { Commands.Stop, "/stop" },
            { Commands.Test, "/test" },
            { Commands.Result, "/result" },
            { Commands.Help, "/help" },
            { Commands.Doc_1, "/1" },
            { Commands.Doc_2, "/2" },
            { Commands.Doc_3, "/3" },
            { Commands.Doc_4, "/4" },
            { Commands.Doc_5, "/5" },
            { Commands.Link_1, "/6" },
            { Commands.Video_1, "/7" },

        };

        private readonly HashSet<string> _testOptionAnswers = new HashSet<string>() { "1", "2", "3", "4" };

        public Bot(string idTelegramBot)
        {
            _idTelegramBot = idTelegramBot;
            _cts = new CancellationTokenSource();
            _client = new TelegramBotClient(idTelegramBot);
            _containerQ = JsonSerializer.Deserialize<QuestionContainer>(System.IO.File.ReadAllText(JsonFileName));
        }

        public void Start()
        {
            if (_isStarting)
            {
                Console.WriteLine("Bot yet is started");
                return;
            }

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };

            Console.WriteLine("Bot is Starting");
            _client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );        
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                // Only process Message updates: https://core.telegram.org/bots/api#message
                if (update.Type != UpdateType.Message
                    || update.Message is null
                    || update.Message.Text is null)
                {
                    Console.WriteLine("message is null or message.Text is null or not Message");
                    await botClient.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Бот работает только с текстом, если нужна помощь введите команду: /help или /start",
                        cancellationToken: cancellationToken);
                    return;
                }


                var message = update.Message;
                var messageText = message.Text;
                var chatId = message.Chat.Id;
                Console.WriteLine($"Received a '{messageText}' message in chat {chatId}. Name: {message.Chat.FirstName ?? "anon"}");

                if (!_userDictionaryTesting.TryGetValue(chatId, out var model))
                {
                    model = new ModelUserTest();
                    _userDictionaryTesting.Add(chatId, model);
                }
                else if (messageText.ToLower() == _dictCommands[Commands.Test])
                {
                    model.Reset();
                    model.IsTesting = true;
                    await botClient.SendTextMessageAsync(message.Chat, "Тест начинается, приготовьтесь, будет 15 вопросов, в каждом вопросе нужно дать один правильный ответ.");

                    model.NextQuestion();
                    var question = _containerQ.Questions[model.CurrentQuestionNumber.ToString()];
                    using (var fileStream = new FileStream(question.Path, FileMode.Open, FileAccess.Read))
                    {
                        var imgFile = InputFile.FromStream(fileStream);
                        await botClient.SendPhotoAsync(chatId: chatId,
                                    photo: imgFile,
                                    caption: $"<b>{question.Title}</b>.",
                                    parseMode: ParseMode.Html,
                                    cancellationToken: cancellationToken);
                    }
                    return;

                }
                else if (messageText.ToLower() == _dictCommands[Commands.Stop])
                {
                    await StopMethodTesting();
                    return;
                }



                if (model.IsTesting)
                {
                    //проверить верный ли ответ
                    if (!_testOptionAnswers.Contains(messageText))
                    {
                        await botClient.SendTextMessageAsync(message.Chat, $"Вы ввели недопустимый ответ, можно вводить значения только: {string.Join(", ", _testOptionAnswers)}." + $"\nВведите ответ или введите команду: /stop");
                        return;
                    }
                    var question = _containerQ.Questions[model.CurrentQuestionNumber.ToString()];
                    await botClient.SendTextMessageAsync(message.Chat, question.Answer == messageText ? "Верно" : "Неверно, ответ был: " + question.Answer);
                    if (question.Answer == messageText)
                        model.CountCorrectAnswers++;

                    model.NextQuestion();
                    if (model.IsEndTest())
                    {
                        await botClient.SendTextMessageAsync(message.Chat, "Тест завершен.");
                        await StopMethodTesting();
                        return;
                    }

                    question = _containerQ.Questions[model.CurrentQuestionNumber.ToString()];
                    await SendPhoto(question.Path, question.Title);
                    return;
                }


                if (messageText.ToLower() == _dictCommands[Commands.Start])
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Добро пожаловать в программу для тестирования специалистов по охране труда, для получения " +
                        "списка доступных команд отправьте: /help");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Help])
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Для того чтобы начать тестирование, введите команду: /test" +
                            "\nДля того чтобы остановить тестирование, введите команду: /stop" +
                            "\nДля того чтобы узнать список команд введите команду: /help" +
                            "\nДля того чтобы получить результаты последнего тестирования, введите команду: /result" +
                            "\n\nДля того чтобы получить 'Классификатор причин несчастных случаев на производстве', введите команду: /1" +
                            "\n\nДля того чтобы получить 'Классификатор видов несчастных случаев на производстве', введите команду: /2" +
                            "\n\nДля того чтобы получить 'Протокол опроса пострадавшего', введите команду: /3" +
                            "\n\nДля того чтобы получить 'Подборка документов для учёта и расследования микротравм', введите команду: /4" +
                            "\n\nДля того чтобы получить 'Извещение о несчастном случае', введите команду: /5" +
                            "\n\nДля того чтобы получить статью на тему 'Внеплановый инструктаж по охране труда', введите команду: /6" +
                            "\n\nДля того чтобы получить видео по теме 'Вводный инструктаж по охране труда', введите команду: /7");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Result])
                {
                    await botClient.SendTextMessageAsync(message.Chat, $"Вы ответили на {model.CountCorrectAnswers} вопросов из {model.CountQuestions}, ваш результат: TODO");
                    return;
                }


                if (messageText.ToLower() == _dictCommands[Commands.Doc_1])
                {
                    await SendDoc("Классификатор_причин_несчастных_случаев_на_производстве.docx", "Классификатор причин несчастных случаев на производстве");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Doc_2])
                {
                    await SendDoc("Классификатор_видов_несчастных_случаев_на_производстве.docx", "Классификатор видов несчастных случаев на производстве");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Doc_3])
                {
                    await SendDoc("Протокол_опроса_пострадавшего.docx", "Протокол опроса пострадавшего");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Doc_4])
                {
                    await SendDoc("Подборка_документов_для_учёта_и_расследования_микротравм.doc", "Подборка документов для учёта и расследования микротравм");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Doc_5])
                {
                    await SendDoc("Извещение_о_несчастном_случае.docx", "Извещение о несчастном случае");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Video_1])
                {
                    await botClient.SendTextMessageAsync(chatId, "https://youtu.be/Dwy9QWYY9Rw");
                    return;
                }
                if (messageText.ToLower() == _dictCommands[Commands.Link_1])
                {
                    await botClient.SendTextMessageAsync(chatId, "https://beltrud.ru/vneplanovyj-instruktazh-po-ohrane-truda/");
                    return;
                }


                await botClient.SendTextMessageAsync(chatId: chatId,
                text: "Введите команду: /start" +
                    "\nВаша команда не поддерживается системой, список команд доступен по команде: /help",
                cancellationToken: cancellationToken);

                async Task StopMethodTesting()
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Вы завершили тестирование, ваш результат." +
                            $"\nВы правильно ответили на {model.CountCorrectAnswers} вопросов из {model.CountQuestions}, ваш результат: " + GetMarkForTest());
                    model.IsTesting = false;
                }

                async Task SendDoc(string fileName, string title)
                {
                    using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        var doc = InputFile.FromStream(fileStream);
                        await botClient.SendDocumentAsync(chatId: chatId,
                                    document: doc,
                                    caption: $"<b>{title}</b>.",
                                    parseMode: ParseMode.Html,
                                    cancellationToken: cancellationToken);
                    }
                }

                async Task SendPhoto(string path, string title)
                {
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var imgFile = InputFile.FromStream(fileStream);
                        await botClient.SendPhotoAsync(chatId: chatId,
                                    photo: imgFile,
                                    caption: $"<b>{title}</b>.",
                                    parseMode: ParseMode.Html,
                                    cancellationToken: cancellationToken);
                    }
                }

                int GetMarkForTest()
                {
                    var percent = (model.CountCorrectAnswers * 100.0) / model.CountQuestions;
                    if (percent >= 85.0)
                        return 5;
                    else if (percent >= 65)
                        return 4;
                    else if (percent >= 51)
                        return 3;
                    else
                        return 2;

                }

            }
            catch (Exception ex)
            {
                _ = await botClient.SendTextMessageAsync(update.Message.Chat, "Команда в данный момент недоступна, обратитесь к администратору, приносим извинения ");
                Console.WriteLine(ex);
            }
        }


        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }

    class ModelUserTest
    {
        public string LastQuestion { get; set; } = string.Empty;

        public int CountCorrectAnswers { get; set; }

        public int CurrentQuestionNumber { get; set; }

        public int CountQuestions { get; set; } = 10;

        public bool IsTesting { get; set;} 

        public void Reset()
        {
            LastQuestion = string.Empty;
            CountCorrectAnswers = 0;
            IsTesting = false;
            CurrentQuestionNumber = 0;
        }

        public bool IsEndTest() => CurrentQuestionNumber > CountQuestions;

        public void NextQuestion()
        {
            CurrentQuestionNumber++;
        }
    }

    public class QuestionContainer
    {
        public Dictionary<string, QuestionItem> Questions { get; set; }

        public class QuestionItem
        {
            public string Path { get; set; }
            public string Title { get; set; }
            public string Answer { get; set; }

        }
    }
    

    enum Commands
    {
        Start,
        Test,
        Stop,
        Result,
        Help,
        Doc_1,
        Doc_2,
        Doc_3,
        Doc_4,
        Doc_5,
        Link_1,
        Video_1,
    }
}
