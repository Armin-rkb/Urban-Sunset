using UnityEngine;

namespace UrbanSunset
{
    public class AudioManager : MonoBehaviour
    {
        AudioSource music;
        AudioSource[] pool;
        int next;
        string currentMusic;
        bool muted;

        public bool Muted { get { return muted; } }

        void Awake()
        {
            gameObject.AddComponent<AudioListener>();
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0.55f;
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            muted = PlayerPrefs.GetInt("muted", 0) == 1;
            AudioListener.volume = muted ? 0f : 1f;
        }

        public void ToggleMute()
        {
            muted = !muted;
            AudioListener.volume = muted ? 0f : 1f;
            PlayerPrefs.SetInt("muted", muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void PlayMusic(string name, float volume = 0.55f)
        {
            if (currentMusic == name && music.isPlaying) return;
            currentMusic = name;
            music.clip = GameAssets.Clip(name);
            music.volume = volume;
            music.pitch = 1f;
            if (music.clip != null) music.Play();
        }

        public void StopMusic()
        {
            music.Stop();
            currentMusic = null;
        }

        public void SetMusicPitch(float pitch) { music.pitch = pitch; }

        public void Sfx(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0f)
        {
            var clip = GameAssets.Clip(name);
            if (clip == null) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            src.volume = volume;
            src.clip = clip;
            src.Play();
        }
    }
}
