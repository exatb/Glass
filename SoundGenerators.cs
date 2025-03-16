using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundGenerators
{
    /// <summary>
    /// Базовый интерфейс генератора одного сэмпла (возвращает сэмпл на момент времени currentTime)
    /// </summary>
    public interface ISoundGenerator
    {
        float GenerateSample(float currentTime);
    }

    /// <summary>
    /// Простая реализация генератора белого шума
    /// </summary>
    public class WhiteNoiseGenerator : ISoundGenerator
    {
        private readonly Random _rand = new Random();
        private readonly float _amplitude;

        public WhiteNoiseGenerator(float amplitude = 0.3f)
        {
            _amplitude = amplitude;
        }

        // Генерация отсчёта шума
        public float GenerateSample(float currentTime)
        {
            // Пример белого шума в диапазоне [-_amplitude, +_amplitude]
            return (float)(_rand.NextDouble() * 2.0 - 1.0) * _amplitude;
        }
    }

    /// <summary>
    /// Генератор тонального (синусоидального) сигнала с экспоненциальным затуханием.
    /// </summary>
    public class ExponentialSineGenerator : ISoundGenerator
    {
        /// <summary> Амплитуда (максимальное значение при t=0) </summary>
        public float Amplitude { get; set; } = 1.0f;

        /// <summary> Частота синусоиды (Гц) </summary>
        public float Frequency { get; set; } = 440f;

        /// <summary> Начальная фаза (в радианах) </summary>
        public float InitialPhase { get; set; } = 0f;

        /// <summary> Время начала звучания (в секундах), до этого сигнал = 0 </summary>
        public float StartTime { get; set; } = 0f;

        /// <summary> Время затухания (в секундах). Чем меньше, тем быстрее затухает. </summary>
        public float DecayTime { get; set; } = 1f;

        public ExponentialSineGenerator(
            float amplitude,
            float frequency,
            float initialPhase,
            float startTime,
            float decayTime)
        {
            Amplitude = amplitude;
            Frequency = frequency;
            InitialPhase = initialPhase;
            StartTime = startTime;
            DecayTime = decayTime;
        }

        /// <summary>
        /// Возвращает один отсчёт синусоиды с экспоненциальным затуханием.
        /// </summary>
        public float GenerateSample(float currentTime)
        {
            float t = currentTime - StartTime;

            // Если сигнал ещё не начался
            if (t < 0f)
                return 0f;

            // Можно оборвать сигнал
            // if (t > DecayTime * 5f) return 0f; 

            // Экспоненциальная огибающая
            float envelope = Amplitude * MathF.Exp(-t / DecayTime);

            // Синусоида
            float sample = envelope * MathF.Sin(2f * MathF.PI * Frequency * t + InitialPhase);

            return sample;
        }
    }

    /// <summary>
    /// Генератор белого шума с экспоненциальным затуханием.
    /// </summary>
    public class ExponentialNoiseGenerator : ISoundGenerator
    {
        /// <summary> Начальная максимальная амплитуда шума </summary>
        public float Amplitude { get; set; } = 1.0f;

        /// <summary> Время начала (с), до которого сигнал = 0 </summary>
        public float StartTime { get; set; } = 0f;

        /// <summary> Время экспоненциального затухания (с) </summary>
        public float DecayTime { get; set; } = 1f;

        // Для генерации белого шума
        private readonly Random _rand = new Random();

        public ExponentialNoiseGenerator(
            float amplitude,
            float startTime,
            float decayTime)
        {
            Amplitude = amplitude;
            StartTime = startTime;
            DecayTime = decayTime;
        }

        public float GenerateSample(float currentTime)
        {
            float t = currentTime - StartTime;
            if (t < 0f)
                return 0f;

            // Экспоненциальная огибающая
            float envelope = Amplitude * MathF.Exp(-t / DecayTime);

            // Генерация белого шума в диапазоне [-1..+1]
            float noise = (float)(_rand.NextDouble() * 2.0 - 1.0);

            return envelope * noise;
        }
    }
}
