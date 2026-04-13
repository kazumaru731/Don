using UnityEngine;

namespace DonGame2D
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [Header("Audio Clips")]
        public AudioClip drawSound; // 引く01
        public AudioClip dealSound; // 引く02
        public AudioClip donSound;  // 出す01
        public AudioClip playSound; // 出す02

        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void PlayDraw() { if (audioSource != null && drawSound != null) audioSource.PlayOneShot(drawSound); }
        public void PlayDeal() { if (audioSource != null && dealSound != null) audioSource.PlayOneShot(dealSound); }
        public void PlayDon()  { if (audioSource != null && donSound != null) audioSource.PlayOneShot(donSound); }
        public void PlayPlay() { if (audioSource != null && playSound != null) audioSource.PlayOneShot(playSound); }
    }
}
