using System;

namespace tgbot.Models
{
    public static class HealthCalculator
    {
        // 1. Розрахунок ІМТ (BMI)
        public static double CalculateBMI(double weight, double height) 
            => weight / Math.Pow(height / 100, 2);

        // 2. Інтерпретація ІМТ
        public static string GetBMICategory(double bmi) => bmi switch
        {
            < 18.5 => "Недостатня вага 🦴",
            < 25 => "Норма ✅",
            < 30 => "Надмірна вага ⚠️",
            _ => "Ожиріння 🚨"
        };

        // 3. Формула Міффліна-Сан Жеора (Базальний метаболізм)
        public static double CalculateBMR(UserData user)
        {
            // Для чоловіків: 10*вага + 6.25*зріст - 5*вік + 5
            return (10 * user.Weight) + (6.25 * user.Height) - (5 * user.Age) + 5;
        }

        // 4. Розрахунок спалених калорій (MET формула)
        public static double CalculateBurnedCalories(string exerciseName, double weight, int durationSec)
        {
            double met = exerciseName switch
            {
                "Бурпі" => 10.0, 
                "Спринти на місці" => 12.0,
                "Віджимання" => 8.0,
                "Планка" => 3.0,
                "Біг на місці" => 9.0, 
                "Розтяжка ніг" => 2.5,
                "Альпініст" => 10.0,
                "Стрибки" => 8.0,
                _ => 5.0 // Середнє значення для інших вправ
            };

            double durationMin = durationSec / 60.0;
            // Наукова формула: (MET * 3.5 * вага / 200) * тривалість у хвилинах
            return (met * 3.5 * weight / 200) * durationMin;
        }

        // 5. Розумні поради на основі показників
        public static string GetPersonalAdvice(double bmi)
        {
            return bmi switch
            {
                < 18.5 => "💡 Порада: Зосередьтеся на силових вправах 💪 та збільште споживання білків.",
                < 25 => "💡 Порада: Ви у чудовій формі! Підтримуйте активність різноманітними вправами 🧘.",
                _ => "💡 Порада: Рекомендуємо додати більше кардіо ❤️ та HIIT ⚡ для ефективного спалювання калорій."
            };
        }
    }
}