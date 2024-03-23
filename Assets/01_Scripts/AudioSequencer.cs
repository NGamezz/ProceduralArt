using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class AudioSequencer : MonoBehaviour
{
    [SerializeField] private AudioClipHolder[] clips;

    [SerializeField] private int amountOfLines = 3;
    [SerializeField] private int lineLenght = 4;
    [SerializeField] private int bpm = 240;
    [SerializeField] private int sequenceRepeatCount = 5;
    [SerializeField] private bool infiniteRepeat = false;

    [SerializeField, Range(0, 1)] private float treshHoldForNote = 0.5f;

    [SerializeField] private AudioClip silence;

    private uint sequenceIndex = 0;

    AudioSource[] audioSources;

    private void Start ()
    {
        amountOfLines = clips.Length;

        audioSources = new AudioSource[amountOfLines];

        SetupAudioSource();

        StartSequence();
    }

    private async void StartSequence ()
    {
        await PlaySequence();
        sequenceIndex++;
        if ( (sequenceIndex < sequenceRepeatCount && !infiniteRepeat) || infiniteRepeat )
        {
            StartSequence();
        }
    }

    private void SetupAudioSource ()
    {
        for ( int i = 0; i < amountOfLines; i++ )
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            var ran = Random.Range(-1.0f, 1.0f);
            source.panStereo = ran;

            source.volume = clips[i].volume;

            audioSources[i] = source;
        }
    }

    private async Task PlaySequence ()
    {
        var lines = new List<SequencerLine>();

        //initial Values
        for ( int i = 0; i < amountOfLines; i++ )
        {
            SequencerLine line = new(lineLenght, bpm, clips[i].audioClips, silence, clips[i].weight, new(sequenceIndex + i, sequenceIndex + i), treshHoldForNote);
            lines.Add(line);

            var ran = Random.Range(-1.0f, 1.0f);
            audioSources[i].panStereo = ran;
        }

        //Second Pass
        for ( int i = 0; i < lines.Count; i += 2 )
        {
            if ( i + 1 >= lines.Count )
                continue;

            var firstLine = lines[i];
            var secondLine = lines[i + 1];

            for ( int t = 0; t < firstLine.line.Length; t++ )
            {
                if ( secondLine.line[i].active )
                {
                    firstLine.line[i].clip = firstLine.line[i].secondClip;
                }
            }
        }

        Task[] tasks = new Task[lines.Count];
        for ( int i = 0; i < lines.Count; i++ )
        {
            tasks[i] = PlaySequencerAsync(lines[i], audioSources[i]);
        }

        await Task.WhenAll(tasks);
    }

    //Has an odd space in between the first and second note.
    private async Task PlaySequencerAsync ( SequencerLine line, AudioSource source )
    {
        foreach ( var note in line.line )
        {
            source.clip = note.clip;
            source.Play();
            await Awaitable.WaitForSecondsAsync(1 / (line.Bpm / 60.0f));
        }
    }
}

//For storing potentially more info in each note.
public struct SequencerNote
{
    public AudioClip clip;
    public AudioClip secondClip;
    public bool active;

    public SequencerNote ( AudioClip clip, bool active, AudioClip secondClip )
    {
        this.active = active;
        this.clip = clip;
        this.secondClip = secondClip;
    }
}

public struct SequencerLine
{
    public SequencerNote[] line;
    public float Bpm;

    public SequencerLine ( int lenght, float bpm, AudioClip[] clips, AudioClip silence, float weight, float2 offSet, float noteTreshHold )
    {
        line = new SequencerNote[lenght];

        for ( int i = 0; i < line.Length; i++ )
        {
            var ranVal = Mathf.PerlinNoise1D(((float)i + offSet.x) / line.Length * weight);

            var clip = clips[Random.Range(0, clips.Length)];
            if ( ranVal < noteTreshHold )
            {
                line[i] = new(silence, false, clip);
            }
            else
            {
                line[i] = new(clip, true, silence);
            }
        }

        Bpm = bpm;
    }
}

[Serializable]
public struct AudioClipHolder
{
    public float volume;
    public AudioClip[] audioClips;
    public float weight;
}