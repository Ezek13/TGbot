using System;
using System.Collections.Generic;
using System.Linq;

namespace tgbot.Models
{
    public static class StatisticsService
    {
        public static string GetUserReport(UserData user)
        {
            if (user.CompletedExercises == null || !user.CompletedExercises.Any())
                return "📭 Ви ще не виконали жодної вправи. Час почати тренування!";

            // Рахуємо загальну кількість
            int total = user.CompletedExercises.Count;

            // Рахуємо вправи за сьогодні
            int today = user.CompletedExercises
                .Count(e => e.Date.Date == DateTime.Today);

            // Топ-3 найпопулярніших вправ користувача
            var topExercises = user.CompletedExercises
                .GroupBy(e => e.ExerciseName)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} ({g.Count()} разів)");

            string report = $"📈 *Ваш прогрес:*\n\n" +
                            $"✅ Всього виконано: {total}\n" +
                            $"🔥 Сьогодні: {today}\n\n" +
                            $"🏆 *Топ вправ:* \n{string.Join("\n", topExercises)}";
            
            return report;
        }
    }
}