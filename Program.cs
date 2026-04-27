using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using tgbot.Models;

namespace tgbot
{
    class Program
    {
        private static ITelegramBotClient botClient = new TelegramBotClient("8570826679:AAGs4geWPyji9x217DQ69K-eco48XKQcOpA");
        private const string DbPath = "users.json";
        private static int _activeTrainingsCount = 0; // Лічильник для консолі

        private static readonly Dictionary<string, (int WorkSec, int RestSec, string Category)> ExerciseData = new()
        {
            { "Віджимання", (45, 30, "Силові 💪") },
            { "Присідання", (45, 30, "Силові 💪") },
            { "Випади", (45, 30, "Силові 💪") },
            { "Планка", (60, 30, "Силові 💪") },
            { "Підйом тазу", (45, 30, "Силові 💪") },
            { "Біг на місці", (60, 20, "Кардіо ❤️") },
            { "Стрибки", (45, 15, "Кардіо ❤️") },
            { "Швидка ходьба", (120, 30, "Кардіо ❤️") },
            { "Танці", (180, 60, "Кардіо ❤️") },
            { "Бурпі", (30, 15, "HIIT ⚡") },
            { "Спринти на місці", (20, 10, "HIIT ⚡") },
            { "Альпініст", (40, 20, "HIIT ⚡") },
            { "Присідання зі стрибком", (30, 15, "HIIT ⚡") },
            { "Планка з рухами", (45, 20, "Функціональні 🧘") },
            { "Випади з поворотом", (45, 20, "Функціональні 🧘") },
            { "Баланс на одній нозі", (40, 15, "Функціональні 🧘") },
            { "Повільні присідання", (60, 30, "Функціональні 🧘") },
            { "Розтяжка ніг", (40, 10, "Розтяжка 🧩") },
            { "Розтяжка спини", (40, 10, "Розтяжка 🧩") },
            { "Нахили вперед", (30, 10, "Розтяжка 🧩") },
            { "Рухливість суглобів", (60, 0, "Розтяжка 🧩") },
            { "Легка йога", (300, 60, "Розтяжка 🧩") }
        };

        static async Task Main()
        {
            using var cts = new CancellationTokenSource();
            botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, new ReceiverOptions(), cts.Token);
            LoggerService.Log("Бот запущений (Professional Mode)...");
            UpdateConsoleStatus();
            await Task.Delay(-1);
        }

        static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
{
    try 
    {
        // 1. ОБРОБКА НАТИСКАННЯ НА INLINE-КНОПКИ (CallbackQuery)
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
        {
            if (update.CallbackQuery.Data == "water_drunk")
            {
                var allUsers = LoadData();
                var waterUser = allUsers.Find(u => u.ChatId == update.CallbackQuery.Message.Chat.Id);
                
                if (waterUser != null)
                {
                    waterUser.Experience += 5; // Бонусні XP за воду
                    SaveData(allUsers);
                    
                    // Повідомляємо серверу Telegram, що запит оброблено (прибирає "годинничок" на кнопці)
                    await bot.AnswerCallbackQuery(update.CallbackQuery.Id, "Чудово! +5 XP нараховано 💧");
                    
                    // Редагуємо повідомлення, щоб кнопка зникла після натискання
                    await bot.EditMessageText(
                        chatId: waterUser.ChatId,
                        messageId: update.CallbackQuery.Message.MessageId,
                        text: "✅ Ви успішно поповнили водний баланс! (+5 XP)",
                        cancellationToken: ct
                    );
                }
            }
            return; // Виходимо, щоб не обробляти це як текстове повідомлення
        }

        // 2. ПЕРЕВІРКА НА ТЕКСТОВЕ ПОВІДОМЛЕННЯ
        if (update.Message is not { Text: { } messageText } message) return;
        long chatId = message.Chat.Id;

        var users = LoadData();
        var user = users.Find(u => u.ChatId == chatId) ?? new UserData { ChatId = chatId, IsMusicEnabled = true };

        // Логіка реєстрації (якщо користувач у процесі введення даних)
        if (user.CurrentState != "None" && user.CurrentState != "Completed")
        {
            await ProcessRegistration(bot, user, messageText, ct);
            SaveData(users);
            return;
        }

        // Перемикач музики
        if (messageText.Contains("Музика"))
        {
            user.IsMusicEnabled = !user.IsMusicEnabled;
            string status = user.IsMusicEnabled ? "увімкнено ✅" : "вимкнено ❌";
            await bot.SendMessage(chatId, $"🎧 Режим музики {status}!", replyMarkup: GetMainMenu(user), cancellationToken: ct);
        }
        else
        {
            switch (messageText)
            {
                case "/start":
                case "Головне меню 🏠":
                    await bot.SendMessage(chatId, "Оберіть потрібний розділ:", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    break;

                case "Ввести дані 📝":
                    user.CurrentState = "WaitingName";
                    await bot.SendMessage(chatId, "Як вас звати?", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    break;

                case "Мій профіль 👤":
                    if (string.IsNullOrEmpty(user.Name)) {
                        await bot.SendMessage(chatId, "❌ Профіль порожній. Заповніть дані!", replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    } else {
                        double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
        
                        // Формуємо гарний текст з рангом
                        string profile = $"📋 *ВАШ ПРОФІЛЬ*\n" +
                                         $"━━━━━━━━━━━━━━━\n" +
                                         $"👤 *Ім'я:* {user.Name}\n" +
                                         $"🎖 *Ранг:* {user.Rank}\n" + // Твій новий ранг!
                                         $"🏆 *Рівень:* {user.Level} (До наст.: {100 - (user.Experience % 100)} XP)\n" +
                                         $"🔥 *Серія:* {user.StreakCount} днів\n" +
                                         $"━━━━━━━━━━━━━━━\n" +
                                         $"📏 *Зріст:* {user.Height} см | ⚖️ *Вага:* {user.Weight} кг\n" +
                                         $"📊 *ВМІ:* {Math.Round(bmi, 1)} ({HealthCalculator.GetBMICategory(bmi)})\n" +
                                         $"🎵 *Музика:* {(user.IsMusicEnabled ? "✅" : "❌")}";

                        await bot.SendMessage(chatId, profile, parseMode: ParseMode.Markdown, replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    }
                    break;

                case "Таблиця лідерів 🏆":
                    // Сортуємо користувачів за досвідом (LINQ)
                    var topUsers = users
                        .OrderByDescending(u => u.Experience)
                        .Take(5) // Беремо топ-5
                        .ToList();

                    string leaderboard = "🏆 *ТОП-5 АТЛЕТІВ KPI:*\n\n";
                    for (int i = 0; i < topUsers.Count; i++)
                    {
                        string medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => "👤" };
                        leaderboard += $"{medal} {topUsers[i].Name ?? "Анонім"} — {topUsers[i].Experience} XP\n";
                    }

                    if (!topUsers.Any()) leaderboard = "📭 Таблиця лідерів поки порожня.";

                    await bot.SendMessage(chatId, leaderboard, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;

                case "Здоров'я 🍎":
                    if (user.Weight == 0 || user.Height == 0) 
                    {
                        await bot.SendMessage(chatId, "⚠️ Спочатку заповніть профіль (вага та зріст), щоб я міг розрахувати показники!", 
                            replyMarkup: GetMainMenu(user), cancellationToken: ct);
                    } 
                    else 
                    {
                        // Попередні розрахунки для зручності
                        double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
                        string bmiCategory = HealthCalculator.GetBMICategory(bmi);
                        double bmr = HealthCalculator.CalculateBMR(user);
        
                        // Розрахунок калорій для підтримки ваги (BMR * середній коефіцієнт активності 1.2)
                        double maintenanceCalories = bmr * 1.2; 
        
                        // Отримуємо розумну пораду на основі ІМТ
                        string personalAdvice = HealthCalculator.GetPersonalAdvice(bmi);

                        string healthInfo = $"🍎 *ВАШІ ПОКАЗНИКИ ЗДОРОВ'Я*\n" +
                                            $"━━━━━━━━━━━━━━━━━━\n" +
                                            $"📊 *ІМТ:* {Math.Round(bmi, 1)} — _{bmiCategory}_\n" +
                                            $"🔥 *Базовий метаболізм:* {Math.Round(bmr)} ккал\n" +
                                            $"🏃 *Норма для підтримки ваги:* {Math.Round(maintenanceCalories)} ккал\n" +
                                            $"💧 *Денна норма води:* {Math.Round(user.Weight * 0.03, 1)} л\n" +
                                            $"━━━━━━━━━━━━━━━━━━\n" +
                                            $"{personalAdvice}\n\n" +
                                            $"💡 *Випадкова порада:* _{HealthService.GetRandomTip()}_";

                        await bot.SendMessage(chatId, healthInfo, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    }
                    break;
                case "Статистика 📊":
                    string statsReport = StatisticsService.GetUserReport(user);
                    await bot.SendMessage(chatId, statsReport, parseMode: ParseMode.Markdown, cancellationToken: ct);
                    break;

                case "Тренування 🏋️":
                case "Назад до тренувань 🔙":
                    await bot.SendMessage(chatId, "Оберіть категорію:", replyMarkup: GetTrainingMenu(), cancellationToken: ct);
                    break;

                case "Силові 💪": await bot.SendMessage(chatId, "💪 Оберіть силову вправу:", replyMarkup: GetStrengthMenu(), cancellationToken: ct); break;
                case "Кардіо ❤️": await bot.SendMessage(chatId, "❤️ Оберіть кардіо:", replyMarkup: GetCardioMenu(), cancellationToken: ct); break;
                case "HIIT ⚡": await bot.SendMessage(chatId, "⚡ Оберіть HIIT вправу:", replyMarkup: GetHIITMenu(), cancellationToken: ct); break;
                case "Функціональні 🧘": await bot.SendMessage(chatId, "🧘 Оберіть вправу:", replyMarkup: GetFunctionalMenu(), cancellationToken: ct); break;
                case "Розтяжка 🧩": await bot.SendMessage(chatId, "🧩 Оберіть вправу:", replyMarkup: GetStretchMenu(), cancellationToken: ct); break;
                case "Авто-підбір 🤖": await SuggestTraining(bot, user, ct); break;
                
                default:
                    if (ExerciseData.ContainsKey(messageText)) 
                        await SendExerciseVideo(bot, user, messageText, ct);
                    break;
            }
        }

        // Зберігаємо зміни
        if (!users.Exists(u => u.ChatId == chatId)) users.Add(user);
        SaveData(users);
    }
    catch (Exception ex)
    {
        LoggerService.Log($"Critical error in HandleUpdateAsync: {ex.Message}");
    }
}

        private static async Task SendExerciseVideo(ITelegramBotClient bot, UserData user, string exName, CancellationToken ct)
        {
            if (ExerciseData.TryGetValue(exName, out var data)) 
            {
                try 
                {
                    // Отримуємо посилання (переконайся, що PexelsService повертає ПРЯМЕ посилання на mp4)
                    string videoUrl = await PexelsService.GetExerciseVideoAsync(exName);
                    string musicInfo = user.IsMusicEnabled ? await SpotifyService.GetMusicForExercise(data.Category) : "Вимкнено";

                    string infoText = $"🎥 *Вправа:* {exName}\n" +
                                      $"⏱ *Час:* {data.WorkSec}с | 🛌 *Відпочинок:* {data.RestSec}с\n\n" +
                                      $"🎧 [Музика для тренування]({musicInfo})";

                    // Використовуємо InputFile.FromUri для надсилання посилання
                    await bot.SendVideo(
                        chatId: user.ChatId,
                        video: InputFile.FromUri(videoUrl), 
                        caption: infoText,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Video Error ({exName}): {ex.Message}");
                    // Якщо відео не пройшло, повідомляємо користувача, але НЕ блокуємо процес
                    await bot.SendMessage(user.ChatId, $"⚠️ Не вдалося завантажити відео для *{exName}*, але ми починаємо тренування!", 
                        parseMode: ParseMode.Markdown, cancellationToken: ct);
                }

                // ЗАПУСК ТАЙМЕРІВ (фоновий для одиночних вправ)
                _ = RunExerciseLifecycle(bot, user.ChatId, data.WorkSec, data.RestSec, exName, ct);
            }
        }

        private static async Task RunExerciseLifecycle(ITelegramBotClient bot, long chatId, int work, int rest, string exName, CancellationToken ct)
{
    Interlocked.Increment(ref _activeTrainingsCount);
    UpdateConsoleStatus();

    try 
    {
        // 1. ТАЙМЕР РОБОТИ (чекаємо завершення)
        await StartLiveTimer(bot, chatId, work, $"🔥 Виконуємо: {exName}", ct);

        // 2. ОНОВЛЕННЯ ДАНИХ ТА КАЛОРІЙ
        var allUsers = LoadData();
        var u = allUsers.Find(usr => usr.ChatId == chatId);
        if (u != null)
        {
            u.Experience += work;
            double burned = HealthCalculator.CalculateBurnedCalories(exName, u.Weight, work);
            
            // Якщо ти додав поле для калорій у UserData, розкоментуй:
            // u.DailyCaloriesBurned += burned; 

            UpdateStreak(u);
            u.CompletedExercises.Add(new ExerciseRecord { ExerciseName = exName, Date = DateTime.Now });
            SaveData(allUsers);

            await bot.SendMessage(chatId, 
                $"✅ *Вправу завершено!*\n" +
                $"🌟 +{work} XP | 🔥 ~{Math.Round(burned, 1)} ккал", 
                parseMode: ParseMode.Markdown, cancellationToken: ct);
        }

        // 3. ТАЙМЕР ВІДПОЧИНКУ (починається ОДРАЗУ після повідомлення про завершення)
        if (rest > 0) 
        {
            await StartLiveTimer(bot, chatId, rest, "🛌 Відпочинок:", ct);
            await bot.SendMessage(chatId, "🔔 Відпочинок закінчено! Готові далі?", cancellationToken: ct);
        }

        // 4. Фонове нагадування про воду (залишаємо у фоні, воно не заважає)
        _ = Task.Run(async () => {
            await Task.Delay(60000); 
            try {
                var waterKeyboard = new InlineKeyboardMarkup(new[] {
                    InlineKeyboardButton.WithCallbackData("Я випив води! 💧", "water_drunk")
                });
                await bot.SendMessage(chatId, "🔔 *Поповніть водний баланс!*", parseMode: ParseMode.Markdown, replyMarkup: waterKeyboard);
            } catch { }
        });
    }
    finally 
    {
        Interlocked.Decrement(ref _activeTrainingsCount);
        UpdateConsoleStatus();
    }
}

        private static void UpdateStreak(UserData user)
        {
            DateTime today = DateTime.Today;
            if (user.LastTrainingDate.Date == today) return;

            if ((today - user.LastTrainingDate.Date).Days == 1)
                user.StreakCount++;
            else
                user.StreakCount = 1;

            user.LastTrainingDate = today;
        }

        private static void UpdateConsoleStatus() => 
            Console.Title = $"Active Workouts: {_activeTrainingsCount} | KPI Fitness Bot";

        private static async Task StartLiveTimer(ITelegramBotClient bot, long chatId, int seconds, string title, CancellationToken ct)
        {
            try 
            {
                var statusMessage = await bot.SendMessage(chatId, $"{title} {seconds}с", cancellationToken: ct);

                for (int i = seconds - 1; i >= 0; i--)
                {
                    await Task.Delay(1000, ct); 
                    if (i % 5 == 0 || i <= 5)
                    {
                        try {
                            await bot.EditMessageText(chatId, statusMessage.MessageId, i > 0 ? $"{title} {i}с" : $"✅ {title} завершено!", cancellationToken: ct);
                        } catch { }
                    }
                }
            } catch { }
        }

        private static ReplyKeyboardMarkup GetMainMenu(UserData user) 
        {
            string musicBtn = user.IsMusicEnabled ? "Музика 🎵 ✅" : "Музика 🎵 ❌";
    
            return new ReplyKeyboardMarkup(new[] 
            { 
                // Перший ряд: Профіль та введення даних
                new KeyboardButton[] { "Ввести дані 📝", "Мій профіль 👤" }, 
        
                // Другий ряд: Тренування та твоє Здоров'я
                new KeyboardButton[] { "Тренування 🏋️", "Здоров'я 🍎" },
        
                // Третій ряд: Статистика та Лідерборд
                new KeyboardButton[] { "Статистика 📊", "Таблиця лідерів 🏆" },
        
                // Четвертий ряд: Керування музикою
                new KeyboardButton[] { musicBtn } 
            }) 
            { 
                ResizeKeyboard = true // Щоб кнопки не були величезними
            };
        }

        private static async Task SuggestTraining(ITelegramBotClient bot, UserData user, CancellationToken ct)
        {
            if (user.Height == 0 || user.Weight == 0) {
                await bot.SendMessage(user.ChatId, "⚠️ Заповніть профіль для авто-підбору!");
                return;
            }
        
            double bmi = HealthCalculator.CalculateBMI(user.Weight, user.Height);
            List<string> selectedExercises = bmi switch {
                < 18.5 => new List<string> { "Віджимання", "Присідання", "Прес" },
                >= 25 => new List<string> { "Бурпі", "Стрибки", "Альпініст" },
                _ => new List<string> { "Планка", "Випади", "Стрибки" }
            };
        
            await bot.SendMessage(user.ChatId, $"🤖 *Авто-підбір активовано!*\nВаш ІМТ: {Math.Round(bmi, 1)}. Підготовлено тренування з {selectedExercises.Count} вправ.", parseMode: ParseMode.Markdown);
        
            _ = Task.Run(async () => 
            {
                foreach (var ex in selectedExercises)
                {
                    if (ExerciseData.TryGetValue(ex, out var data))
                    {
                        await bot.SendMessage(user.ChatId, $"🚀 Наступна вправа: *{ex}*", parseMode: ParseMode.Markdown);
                
                        // ТУТ ми чекаємо завершення ОДНІЄЇ вправи, перш ніж дати наступну
                        // Але оскільки ми всередині Task.Run, весь бот НЕ висить!
                        await RunExerciseLifecycle(bot, user.ChatId, data.WorkSec, data.RestSec, ex, ct);
                    }
                }
                await bot.SendMessage(user.ChatId, "🏆 Комплекс завершено!");
            });
        
            await bot.SendMessage(user.ChatId, "🏆 *Тренування завершено!* Ви молодець!");
        }

        private static async Task ProcessRegistration(ITelegramBotClient bot, UserData user, string text, CancellationToken ct)
        {
            try 
            {
                switch (user.CurrentState)
                {
                    case "WaitingName": user.Name = text; user.CurrentState = "WaitingAge"; await bot.SendMessage(user.ChatId, "Ваш вік?", cancellationToken: ct); break;
                    case "WaitingAge": if (int.TryParse(text, out int a)) { user.Age = a; user.CurrentState = "WaitingHeight"; await bot.SendMessage(user.ChatId, "Ваш зріст (см)?", cancellationToken: ct); } break;
                    case "WaitingHeight": if (double.TryParse(text, out double h)) { user.Height = h; user.CurrentState = "WaitingWeight"; await bot.SendMessage(user.ChatId, "Ваша вага (кг)?", cancellationToken: ct); } break;
                    case "WaitingWeight": if (double.TryParse(text, out double w)) { user.Weight = w; user.CurrentState = "Completed"; await bot.SendMessage(user.ChatId, "Анкету заповнено!", replyMarkup: GetMainMenu(user), cancellationToken: ct); } break;
                }
            } catch { }
        }

        private static ReplyKeyboardMarkup GetTrainingMenu() => new(new[] 
        { 
            new KeyboardButton[] { "Авто-підбір 🤖" },
            new KeyboardButton[] { "Силові 💪", "Кардіо ❤️" }, 
            new KeyboardButton[] { "HIIT ⚡", "Функціональні 🧘" }, 
            new KeyboardButton[] { "Розтяжка 🧩" },
            new KeyboardButton[] { "Головне меню 🏠" } 
        }) { ResizeKeyboard = true };

        private static ReplyKeyboardMarkup GetStrengthMenu() => new(new[] { new KeyboardButton[] { "Віджимання", "Присідання" }, new KeyboardButton[] { "Випади", "Планка" }, new KeyboardButton[] { "Підйом тазу" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetCardioMenu() => new(new[] { new KeyboardButton[] { "Біг на місці", "Стрибки" }, new KeyboardButton[] { "Швидка ходьба", "Танці" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetHIITMenu() => new(new[] { new KeyboardButton[] { "Бурпі", "Спринти на місці" }, new KeyboardButton[] { "Альпініст", "Присідання зі стрибком" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetFunctionalMenu() => new(new[] { new KeyboardButton[] { "Планка з рухами", "Випади з поворотом" }, new KeyboardButton[] { "Баланс на одній нозі", "Повільні присідання" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };
        private static ReplyKeyboardMarkup GetStretchMenu() => new(new[] { new KeyboardButton[] { "Розтяжка ніг", "Розтяжка спини" }, new KeyboardButton[] { "Нахили вперед", "Рухливість суглобів" }, new KeyboardButton[] { "Легка йога" }, new KeyboardButton[] { "Назад до тренувань 🔙" } }) { ResizeKeyboard = true };

        private static List<UserData> LoadData() 
        {
            try { return File.Exists(DbPath) ? JsonSerializer.Deserialize<List<UserData>>(File.ReadAllText(DbPath)) ?? new() : new(); }
            catch { return new List<UserData>(); }
        }

        private static void SaveData(List<UserData> data) 
        {
            try { File.WriteAllText(DbPath, JsonSerializer.Serialize(data)); }
            catch (Exception ex) { LoggerService.Log($"Save error: {ex.Message}"); }
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient b, Exception e, CancellationToken c) 
        {
            LoggerService.Log($"Polling Error: {e.Message}");
            return Task.CompletedTask; 
        }
    }

    public static class LoggerService
    {
        private const string LogFile = "bot_logs.txt";
        public static void Log(string message)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(entry);
            try { File.AppendAllText(LogFile, entry + Environment.NewLine); } catch { }
        }
    }
}