using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Huisheng.PlantMusic
{
    [RequireComponent(typeof(AudioSource), typeof(CircleCollider2D))]
    public class PlantProximitySound : MonoBehaviour
    {
        public AudioClip sound;
        [Range(0,1)] public float volume = .65f;
        public UnityEvent triggered = new UnityEvent();
        public int TriggerCount { get; private set; }
        public bool PlayerInside => contacts.Count > 0;
        public float Pulse { get; private set; }
        readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
        AudioSource source;
        void Awake()
        {
            source=GetComponent<AudioSource>(); source.playOnAwake=false; source.loop=false;
            source.spatialBlend=0;
            GetComponent<CircleCollider2D>().isTrigger=true;
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            if(!other.GetComponentInParent<PlantDemoPlayer>()) return;
            bool wasInside=PlayerInside;
            contacts.Add(other);
            if(wasInside) return;
            if(sound) {source.Stop();source.PlayOneShot(sound,volume);}
            TriggerCount++;Pulse=1;triggered.Invoke();
        }
        void OnTriggerExit2D(Collider2D other){contacts.Remove(other);}
        void Update(){Pulse=Mathf.Max(0,Pulse-Time.deltaTime*2.5f);}
        void OnDisable(){contacts.Clear();if(source)source.Stop();}
    }
}
