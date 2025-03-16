using OpenTK.Audio.OpenAL;
using SoundSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioPlayer
{
    /// <summary>
    /// 
    /// AudioDevice (OpenAL) 
    ///
    /// </summary>
    public class AudioDevice : IDisposable
    {
        private ALDevice _device;
        private ALContext _context;

        public AudioDevice()
        {
            // Открываем устройство
            _device = ALC.OpenDevice(null);
            if (_device == ALDevice.Null)
                throw new Exception("Failed to open default audio device.");

            // Создаём контекст
            ALContextAttributes attrs = new ALContextAttributes();
            _context = ALC.CreateContext(_device, attrs);
            if (_context == ALContext.Null)
                throw new Exception("Failed to create OpenAL context.");

            // Активируем
            ALC.MakeContextCurrent(_context);
        }

        public void Dispose()
        {
            ALC.MakeContextCurrent(ALContext.Null);

            if (_context != ALContext.Null)
            {
                ALC.DestroyContext(_context);
                _context = ALContext.Null;
            }

            if (_device != ALDevice.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;
            }
        }
    }

    /// <summary>
    /// AudioMixer
    /// </summary>
    
    public class AudioMixer : IDisposable
    {
        private AudioDevice _audioDevice;
        private int _source;
        private Queue<int> _buffersQueue = new Queue<int>();

        // Храним список SoundSource
        private List<SoundSource> _sources = new List<SoundSource>();

        // Внутренние
        private float _currentTime = 0f;
        private int _sampleRate;
        private int _bufferSize;

        // WAV запись
        private MemoryStream _wavMem;
        private BinaryWriter _wavWriter;
        private int _totalSamplesWritten;
        public string OutputWavFile { get; set; } = "result.wav";

        public AudioMixer(int sampleRate = 44100, int bufferSize = 1024)
        {
            _sampleRate = sampleRate;
            _bufferSize = bufferSize;

            // 1) Создаём аудиоустройство
            _audioDevice = new AudioDevice();

            // 2) Генерируем источник
            _source = AL.GenSource();

            // 3) Подготовим WAV
            _wavMem = new MemoryStream();
            _wavWriter = new BinaryWriter(_wavMem);
            WriteWavHeaderPlaceholder();
        }

        public void AddSoundSource(SoundSource src)
        {
            _sources.Add(src);
        }

        public void RemoveSoundSource(SoundSource src)
        {
            _sources.Remove(src);
        }

        /// <summary>
        /// Вызывать каждые ~10 мс. Генерирует bufferSize сэмплов,
        /// проигрывает их через OpenAL, параллельно пишет в WAV.
        /// </summary>
        public void Update()
        {
            // Генерируем samples
            short[] samples = new short[_bufferSize];
            for (int i = 0; i < _bufferSize; i++)
            {
                float mixed = 0f;
                // Суммируем по всем SoundSource
                foreach (var src in _sources)
                {
                    mixed += src.GenerateSample(_currentTime);
                }

                samples[i] = FloatToShort(mixed);
                _currentTime += 1f / _sampleRate;
            }

            // Проигрываем в OpenAL
            int bufferId = AL.GenBuffer();
            ALFormat format = ALFormat.Mono16;

            unsafe
            {
                fixed (short* ptr = samples)
                {
                    AL.BufferData(bufferId, format, (IntPtr)ptr, samples.Length * sizeof(short), _sampleRate);
                }
            }

            AL.SourceQueueBuffer(_source, bufferId);
            _buffersQueue.Enqueue(bufferId);

            // Запуск, если не играет
            AL.GetSource(_source, ALGetSourcei.SourceState, out int state);
            if ((ALSourceState)state != ALSourceState.Playing)
            {
                AL.SourcePlay(_source);
            }

            // Удаляем отыгранные буферы
            AL.GetSource(_source, ALGetSourcei.BuffersProcessed, out int processed);
            while (processed-- > 0)
            {
                int oldBuf = AL.SourceUnqueueBuffer(_source);
                AL.DeleteBuffer(oldBuf);
                _buffersQueue.Dequeue();
            }

            // Записываем в WAV
            WriteToWav(samples);

            // Удаляем просроченные компоненты, источники
            Cleanup();
        }

        private void Cleanup()
        {
            // 1) Удаляем просроченные компоненты
            foreach (var src in _sources)
            {
                src.RemoveExpiredComponents(_currentTime);
            }

            // 2) Удаляем сами SoundSource, если помечены и пусты
            _sources.RemoveAll(s => s.CanBeRemoved());
        }

        public void Dispose()
        {
            // Останавливаем
            AL.SourceStop(_source);

            // Удаляем все буферы
            while (_buffersQueue.Count > 0)
            {
                int b = _buffersQueue.Dequeue();
                AL.DeleteBuffer(b);
            }
            AL.DeleteSource(_source);

            _wavWriter.Close();
            _wavMem.Close();

            // Освобождаем аудиоустройство
            _audioDevice.Dispose();
        }

        public void SaveWav()
        {
            // Завершаем WAV
            FinalizeWav();
            File.WriteAllBytes(OutputWavFile, _wavMem.ToArray());
        }

        // ---- Вспомогательное: WAV ----

        private void WriteWavHeaderPlaceholder()
        {
            byte[] blank = new byte[44];
            _wavWriter.Write(blank);
        }

        private void WriteToWav(short[] samples)
        {
            foreach (var s in samples)
                _wavWriter.Write(s);
            _totalSamplesWritten += samples.Length;
        }

        private void FinalizeWav()
        {
            _wavWriter.Seek(0, SeekOrigin.Begin);

            int subchunk2Size = _totalSamplesWritten * sizeof(short);
            int chunkSize = 36 + subchunk2Size;

            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            _wavWriter.Write(chunkSize);
            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            _wavWriter.Write(16); // size
            _wavWriter.Write((short)1); // PCM
            _wavWriter.Write((short)1); // mono
            _wavWriter.Write(_sampleRate);

            int byteRate = _sampleRate * sizeof(short);
            _wavWriter.Write(byteRate);

            short blockAlign = (short)sizeof(short);
            _wavWriter.Write(blockAlign);

            short bitsPerSample = 16;
            _wavWriter.Write(bitsPerSample);

            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            _wavWriter.Write(subchunk2Size);
        }

        private short FloatToShort(float val)
        {
            val = Math.Clamp(val, -1f, 1f);
            return (short)(val * short.MaxValue);
        }
    }
}
