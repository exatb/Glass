using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OpenTK.Audio.OpenAL;
using SoundGenerators;
using AudioFilters;
using SoundComponents;
using SoundSources;
using AudioPlayer;



class Program
{
    static void Main(string[] args)
    {
        int fs = 44100;
        // Создаём микшер
        using var mixer = new AudioMixer(sampleRate: fs, bufferSize: 1024)
        {
            OutputWavFile = "result.wav"
        };

        // Создаём SoundSource, эквивалентный вибрирующей стеклянной сфере 
        // радиуса 1 м, 5 сферических гармоник, 5 радиальных мод
        var sphereSource = SoundSource.CreateVibratingGlassSphereSource(
            startTime: 0f,
            radius: 1f,
            sphericalHarmCount: 5,
            radialModesCount: 5,
            sampleRate: fs,
            baseAmplitude: 1f,
            baseDecay: 0.2f
        );
        // Добавляем в микшер
        mixer.AddSoundSource(sphereSource);

        // Сгенерируем удар по тонкой стеклянной пластине
        // Размер пластины (1m x 1m), 
        // mMax=5, nMax=5, sampleRate=44100
        var plateSource = SoundSource.CreateThinGlassPlateSource(
            startTime: 0f,
            lx: 1f, ly: 1f,
            mMax: 5, nMax: 5,
            sampleRate: 44100,
            baseAmplitude: 0.9f,
            baseDecay: 0.1f
        );
        //Добавляем немного фильтрованного шума для окраски
        plateSource.AddComponent(new SoundComponent
        {
            Generator = new ExponentialNoiseGenerator(0.9f, 0.01f, 0.2f),
            Filter = new BiQuadFilter(BiQuadFilter.FilterType.LowPass, fs, 1000, 1),
            StartTime = 0f,
            Lifetime = 3f
        });

        // Добавляем в микшер
        mixer.AddSoundSource(plateSource);


        // Запускаем цикл ~5с
        float total = 2f;
        var startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalSeconds < total)
        {
            mixer.Update();
            Thread.Sleep(10); // ~20 мс
        }

        // Сохраняем wav
        mixer.SaveWav();

        Console.WriteLine("Готово! Сохранён файл result.wav");
        Console.ReadKey();
    }
}
