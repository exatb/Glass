using SoundComponents;
using SoundGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundSources
{

    /// <summary>
    /// Источник звука:
    ///     - Хранит набор звуковых фрагментов (SoundComponent)
    ///     - Имеет координаты источника и слушателя
    ///     - При изменении координат пересчитывает ослабление
    ///     - При получении команды на удаление может изменить звучание фрагментов
    ///     - Может продолжать звучать после удаления до окончания всех фрагментов
    /// </summary>
    public class SoundSource : ISoundGenerator
    {
        private List<SoundComponent> _components = new List<SoundComponent>();

        // Координаты источника:
        private float _srcX, _srcY, _srcZ;

        // Координаты слушателя:
        private float _listenerX, _listenerY, _listenerZ;

        // Текущее ослабление
        private float _attenuation = 1f;

        // Флаг, что источник помечен на удаление, 
        // но пока есть компоненты – продолжает играть
        private bool _markedForDeletion = false;

        public SoundSource(float sourceX, float sourceY, float sourceZ,
                           float listenerX = 0f, float listenerY = 0f, float listenerZ = 0f)
        {
            _srcX = sourceX; _srcY = sourceY; _srcZ = sourceZ;
            _listenerX = listenerX; _listenerY = listenerY; _listenerZ = listenerZ;
            RecalcAttenuation();
        }

        public void AddComponent(SoundComponent component)
        {
            _components.Add(component);
        }

        /// <summary>
        /// Пометить на удаление. Но звуки доиграют, если ещё активны.
        /// </summary>
        public void MarkForDeletion()
        {
            _markedForDeletion = true;
        }

        /// <summary>
        /// Если нет компонентов и стоит флаг удаления, можно убрать из микшера.
        /// </summary>
        public bool CanBeRemoved()
        {
            return _markedForDeletion && _components.Count == 0;
        }

        // ---- Изменение координат источника ----
        public void SetSourcePosition(float x, float y, float z)
        {
            if (x != _srcX || y != _srcY || z != _srcZ)
            {
                _srcX = x; _srcY = y; _srcZ = z;
                RecalcAttenuation();
            }
        }

        // ---- Изменение координат слушателя ----
        public void SetListenerPosition(float x, float y, float z)
        {
            if (x != _listenerX || y != _listenerY || z != _listenerZ)
            {
                _listenerX = x; _listenerY = y; _listenerZ = z;
                RecalcAttenuation();
            }
        }

        /// <summary>
        /// Для периодической проверки и удаления просроченных компонент
        /// </summary>
        public void RemoveExpiredComponents(float currentTime)
        {
            _components.RemoveAll(c => c.IsExpired(currentTime));
        }

        /// <summary>
        /// Реализация ISoundGenerator: сумма звуковых компонентов * attenuation
        /// </summary>
        public float GenerateSample(float currentTime)
        {
            float sum = 0f;
            for (int i = 0; i < _components.Count; i++)
            {
                sum += _components[i].GenerateSample(currentTime);
            }

            return sum * _attenuation;
        }

        /// <summary>
        /// Пересчёт ослабления, если координаты слушателя или источника изменились
        /// </summary>
        private void RecalcAttenuation()
        {
            float dx = _srcX - _listenerX;
            float dy = _srcY - _listenerY;
            float dz = _srcZ - _listenerZ;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

            // Простой закон: 1 / (1 + dist)
            _attenuation = 1f / (1f + dist);
        }

        /// <summary>
        /// Создаёт SoundSource, эквивалентный (упрощённо) 
        /// вибрирующей стеклянной сфере радиуса <paramref name="radius"/>,
        /// с заданным количеством сферических гармоник <paramref name="sphericalHarmCount"/>
        /// и радиальных мод <paramref name="radialModesCount"/>.
        /// Отбрасывает частоты выше sampleRate/2. 
        /// </summary>
        /// <param name="startTime">Время начала воспроизведения</param>
        /// <param name="radius">Радиус сферы (м)</param>
        /// <param name="sphericalHarmCount">Сколько сферических гармоник учитывать</param>
        /// <param name="radialModesCount">Сколько радиальных мод</param>
        /// <param name="sampleRate">Частота дискретизации (Гц)</param>
        /// <param name="baseAmplitude">Базовая амплитуда</param>
        /// <param name="baseDecay">Базовое время затухания (c)</param>
        /// <returns>Экземпляр SoundSource</returns>
        public static SoundSource CreateVibratingGlassSphereSource(
            float startTime,
            float radius,
            int sphericalHarmCount = 5,
            int radialModesCount = 5,
            int sampleRate = 44100,
            float baseAmplitude = 0.3f,
            float baseDecay = 3f)
        {
            // Скорость звука в стекле (упрощённое)
            const float speedOfSound = 5291f; // м/с (примерно)

            // freqBase = c / (2 * pi * R)
            float freqBase = speedOfSound / (2f * MathF.PI * radius);

            // Создаём SoundSource (позиция (0,0,0))
            var source = new SoundSource(0f, 0f, 0f);

            baseAmplitude = baseAmplitude / (sphericalHarmCount * radialModesCount);

            // Генерируем набор "тональных" компонент:
            // Для n=1..radialModesCount, l=0..(sphericalHarmCount-1)
            for (int n = 1; n <= radialModesCount; n++)
            {
                for (int l = 1; l <= sphericalHarmCount; l++)
                {
                    // Коэффициент alpha ~ (n + l + 1)
                    float alpha = (float)Math.Sqrt(l * (l + 1) + n);
                    // freq = freqBase * alpha
                    float freq = freqBase * alpha;

                    if (freq < sampleRate / 2)
                    {
                        // Амплитуда - убавляем при росте n,l, 
                        // чтобы высокие моды были слабее
                        //float amplitude = baseAmplitude / (alpha);
                        float amplitude = baseAmplitude / (n * l);

                        // decayTime - можем слегка уменьшать при росте alpha
                        float decay = baseDecay / (1f + 0.2f * alpha);

                        // Фаза = 0
                        float initialPhase = 0f;

                        // Создаём генератор
                        var generator = new ExponentialSineGenerator(
                            amplitude,
                            freq,
                            initialPhase,
                            startTime,
                            decayTime: decay
                        );

                        // Добавляем компонент
                        source.AddComponent(new SoundComponent
                        {
                            Generator = generator,
                            Filter = null, // можем добавить фильтр при желании
                            StartTime = 0f,
                            Lifetime = decay * 3f // Lifetime - слегка больше decay
                        });
                    }
                }
            }

            return source;
        }


        /// <summary>
        /// Создаёт SoundSource для тонкой стеклянной пластины размера (lx x ly),
        /// используя упрощённую модель для частот изгибных волн.
        /// Отбрасывая те частоты, что выше sampleRate/2.
        /// </summary>
        /// <param name="startTime">Время начала воспроизведения</param>
        /// <param name="lx">Ширина пластины (м)</param>
        /// <param name="ly">Длина пластины (м)</param>
        /// <param name="mMax">Сколько мод по оси x</param>
        /// <param name="nMax">Сколько мод по оси y</param>
        /// <param name="sampleRate">Частота дискретизации (Гц)</param>
        /// <param name="baseAmplitude">Базовая амплитуда</param>
        /// <param name="baseDecay">Базовое время затухания</param>
        /// <returns>SoundSource со всеми модами</returns>
        public static SoundSource CreateThinGlassPlateSource(
            float startTime,
            float lx, float ly,
            int mMax, int nMax,
            float sampleRate,
            float baseAmplitude = 0.3f,
            float baseDecay = 2f)
        {
            // Макс. воспроизводимая частота 
            float maxFreq = sampleRate / 2f;

            // Создаём SoundSource
            var source = new SoundSource(0f, 0f, 0f);

            baseAmplitude = baseAmplitude / (mMax * nMax);


            // Перебираем m=1..mMax, n=1..nMax
            for (int m = 1; m <= mMax; m++)
            {
                for (int n = 1; n <= nMax; n++)
                {
                    // Упрощённая формула:
                    // f_{m,n} = c/2 * sqrt( (m/Lx)^2 + (n/Ly)^2 )

                    float freq = 31f * MathF.Sqrt((m * m) / (lx * lx) + (n * n) / (ly * ly));

                    //float freq = 31f * ( (m * m) / (lx * lx) + (n * n) / (ly * ly) );


                    // Отсечём, если freq > Nyquist
                    if (freq > maxFreq)
                        continue;

                    // Амплитуда: чем выше freq, тем слабее
                    // (Пример: amplitude ~ baseAmplitude / (1 + 0.1 * freq/1000f) )
                    // Или просто amplitude = baseAmplitude/(m+n).
                    float amplitude = baseAmplitude / (1f + 0.1f * (m + n));

                    // Затухание: пусть выше freq = быстрее затух
                    float decay = baseDecay / (1f + 0.1f * (m + n));

                    // Lifetime = 3 * decay, к примеру
                    float lifetime = 3f * decay;

                    var gen = new ExponentialSineGenerator(
                        amplitude: amplitude,
                        frequency: freq,
                        initialPhase: 0f,
                        startTime,
                        decayTime: decay
                    );

                    source.AddComponent(new SoundComponent
                    {
                        Generator = gen,
                        Filter = null,  // При желании BiQuadFilter
                        StartTime = 0f,
                        Lifetime = lifetime
                    });
                }
            }

            return source;
        }

    }

}
