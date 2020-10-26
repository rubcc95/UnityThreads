using System;https://github.com/rubcc95/UnityThreads
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class UnityThreads : MonoBehaviour
{
    static bool hasInstance = false;

    static Dictionary<string, UnityThread> _threads = new Dictionary<string, UnityThread>();


    public static UnityThread Get(string name) => _threads[name];

    public ICollection<string> Keys => throw new NotImplementedException();

    public ICollection<UnityThread> Values => throw new NotImplementedException();

    public static int Count => _threads.Count;
    

    public static void Add(string name, UnityThread thread)
    {
        if (!hasInstance) Initialize();
        _threads.Add(name, thread);
    }    

    public static UnityThread UpdateThread(string name, UnityAction action, float startTime = 0, float duration = Mathf.Infinity)
    {
        var thread = new UnityThread(action, startTime, duration);
        Add(name, thread);
        return thread;
    }

    public static UnityThread FixedUpdateThread(string name, UnityAction action, float startTime = 0, float duration = Mathf.Infinity)
    {
        var thread = new UnityThread(action, startTime, duration, UnityMethod.FixedUpdate);
        Add(name, thread);
        return thread;
    }

    public static UnityThread LateUpdateThread(string name, UnityAction action, float startTime = 0, float duration = Mathf.Infinity)
    {
        var thread = new UnityThread(action, startTime, duration, UnityMethod.LateUpdate);
        Add(name, thread);
        return thread;
    }

    public static UnityThread OnGUIThread(string name, UnityAction action, float startTime = 0, float duration = Mathf.Infinity)
    {
        var thread = new UnityThread(action, startTime, duration, UnityMethod.OnGUI);
        Add(name, thread);
        return thread;
    }

    public static void Clear() => _threads.Clear();

    public static bool Contains(KeyValuePair<string, UnityThread> item) => _threads.Contains(item);

    public static bool Contains(string key) => _threads.ContainsKey(key);

    public static bool Contains(UnityThread value) => _threads.ContainsValue(value);

    public static bool Remove(string key) => _threads.Remove(key);
    
    public static bool TryGetValue(string key, out UnityThread value) => _threads.TryGetValue(key, out value);


    static void Initialize() => new GameObject("ThreadsSystem", typeof(UnityThreads));

    private void Awake() => DontDestroyOnLoad(this);

    private void OnEnable() => hasInstance = true;

    private void OnDisable() => hasInstance = false;
   
    private void Update()
    {
        foreach (UnityThread thread in _threads.Values)
        {
            thread.NextFrame(UnityMethod.Update);
        }
    }

    private void LateUpdate()
    {
        foreach (UnityThread thread in _threads.Values)
        {
            thread.NextFrame(UnityMethod.LateUpdate);
            thread.UpdateDeltaTime();
        }
        _threads = _threads.Where(thread => !thread.Value.Destroyable).
            ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private void FixedUpdate()
    {        
        foreach (UnityThread thread in _threads.Values)
        {
            thread.NextFrame(UnityMethod.FixedUpdate);
            thread.UpdateFixedDeltaTime();
        }
        _threads = _threads.Where(thread => !thread.Value.Destroyable).
            ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private void OnGUI()
    {
        foreach (UnityThread thread in _threads.Values)
        {
            thread.NextFrame(UnityMethod.OnGUI);
        }
    }
}


public class UnityThread
{    
    public UnityThread(UnityAction action = null, float startTime = 0, float duration = Mathf.Infinity, UnityMethod method = UnityMethod.Update) 
    {
        _current = this;
        _method = method;
        _action.AddListener(action);
        StartTime = startTime;
        Duration = duration;
    }

    public static UnityThread _current;

    UnityMethod _method;

    float _waitTime = 0;

    float _startTime;

    float _endTime;

    bool _ended = false;

    private UnityEvent _action = new UnityEvent();

    public UnityEvent OnStart = new UnityEvent(), 
        OnPause = new UnityEvent(), 
        OnResume = new UnityEvent(), 
        OnWaitStart = new UnityEvent(), 
        OnWaitEnd = new UnityEvent(), 
        OnEnd = new UnityEvent();

    public static UnityThread Current => _current;

    public ThreadProperties Properties { get; set; } = new ThreadProperties();

    public bool Started { get; private set; } = false;    

    public bool DestroyWhenEnded { get; set; } = true;

    public bool Destroyable => DestroyWhenEnded && _ended;

    public bool Paused { get; set; } = false;

    public bool Waiting => _waitTime > 0;

    public float StartTime
    {
        get => _startTime - Time.time;
        set => _startTime = Time.time + value;
    }

    public float Duration
    {
        get => _endTime - _startTime;
        set => _endTime = _startTime + value;
    }

    public float EndTime
    {
        get => _endTime - Time.time;
        set => _endTime = Time.time + value;
    }

    public void StartInmediatly()
    {
        _endTime += Time.time - _startTime;
        _startTime = Time.time;
        Started = false;
    }

    public void End()
    {
        if(!_ended) OnEnd.Invoke();
        _ended = true;
    }

    public void Destroy()
    {
        DestroyWhenEnded = true;
        End();
    }

    public void Pause()
    {
        if(!Paused) OnPause.Invoke();
        Paused = true;        
    }

    public void Resume()
    {
        if(Paused) OnResume.Invoke();
        Paused = false;       
    }

    public void Wait(int seconds)
    {
        if (!Waiting && seconds > 0) OnWaitStart.Invoke();
        _waitTime += seconds;
    }

    public void Restart()
    {
        _ended = false;
        Paused = false;
        _waitTime = 0;
        StartInmediatly();
    }

    public void RestartCompletely()
    {
        Properties.Clear();
        Restart();
    }


        
    public void NextFrame(UnityMethod method)
    {
        if(method == _method)
        {
            _current = this;
            if (StartTime < 0)
            {
                if (!Started)
                {
                    Started = true;
                    OnStart.Invoke();
                }
                if (EndTime < 0)
                {
                    End();
                    return;
                }
                if (!Paused && !Waiting)
                {
                    _action.Invoke();
                }
            }
        }
    }

    public void UpdateDeltaTime()
    {
        if (_method != UnityMethod.FixedUpdate) 
            UpdateTime(Time.deltaTime);
    }

    public void UpdateFixedDeltaTime()
    {
        if (_method == UnityMethod.FixedUpdate) 
            UpdateTime(Time.fixedDeltaTime);
    }

    void UpdateTime(float time)
    {
        if (!Paused)
        {
            
            if (Waiting)
            {
                _waitTime -= time;
                if (_waitTime < 0)
                {
                    _waitTime = 0;
                    OnWaitEnd.Invoke();
                }
            }
        }
    }

    public class ThreadProperties : IDictionary<string, object>
    {
        readonly Dictionary<string, ThreadProperty> _properties = new Dictionary<string, ThreadProperty>();
        
        public ThreadProperty this[string name]
        {
            get
            {
                if (_properties.TryGetValue(name, out var result)) 
                {
                    return result;
                }                
                return ThreadProperty.Empty;
            }
            set
            {
                if(_properties.ContainsKey(name))
                {
                    _properties[name] = value;
                }
                else
                {
                    _properties.Add(name, value);
                }                
            }
        }

        object IDictionary<string, object>.this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Count => _properties.Count;

        public ICollection<string> Keys => _properties.Keys;

        public ICollection<object> Values => _properties.Values.Select(property => property.Value) as ICollection<object>;

        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            var property = ThreadProperty.Empty;
            property.Value = value;
            _properties.Add(key, property);
        }

        public void Add(KeyValuePair<string, object> item) => Add(item.Key, item.Value);


        public void Clear() => _properties.Clear();

        public bool Contains(KeyValuePair<string, object> item)
        {
            var property = ThreadProperty.Empty;
            property.Value = item.Value;
            return _properties.Contains(new KeyValuePair<string, ThreadProperty>(item.Key, property));
        }

        public bool ContainsKey(string key) => _properties.ContainsKey(key);        

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            (_properties.Select(property => new KeyValuePair<string, object>(property.Key, property.Value.Value)) as ICollection<KeyValuePair<string, object>>)
                .CopyTo(array, arrayIndex);            
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _properties.Select(property => new KeyValuePair<string, object>(property.Key, property.Value.Value))
                .GetEnumerator();
        }

        public bool Remove(string key) => _properties.Remove(key);

        public bool Remove(KeyValuePair<string, object> item)
        {
            var found = TryGetValue(item.Key, out var property);
            if (!found || property != item.Value) return false;
            return Remove(item.Key);            
        }

        public bool TryGetValue(string key, out object value)
        {
            var result = _properties.TryGetValue(key, out var property);
            value = property == null ? null : property.Value;
            return result;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        

        public class ThreadProperty
        {
            ThreadProperty(object value)
            {
                _property = value;
            }

            public static ThreadProperty Empty = new ThreadProperty(null);

            public object _property;

            public int Integer => _property == null ? 0 : (int)_property;

            public float Float => _property == null ? 0 : (float)_property;

            public bool Bool => _property == null ? false : (bool)_property;

            public string String => _property == null ? null : (string)_property;

            public Vector2 Vector2 => _property == null ? Vector2.zero : (Vector2)_property;

            public Vector3 Vector3 => _property == null ? Vector3.zero : (Vector3)_property;

            public Quaternion Quaternion => _property == null ? Quaternion.identity : (Quaternion)_property;

            public Color Color => _property == null ? Color.clear : (Color)_property;

            public object Value
            {
                get => _property;
                set => _property = value;
            }

            public static implicit operator bool(ThreadProperty property) => property.Bool;

            public static implicit operator int(ThreadProperty property) => property.Integer;

            public static implicit operator string(ThreadProperty property) => property.String;

            public static implicit operator float(ThreadProperty property) => property.Float;

            public static implicit operator Vector2(ThreadProperty property) => property.Vector2;

            public static implicit operator Vector3(ThreadProperty property) => property.Vector3;

            public static implicit operator Quaternion(ThreadProperty property) => property.Quaternion;

            public static implicit operator Color(ThreadProperty property) => property.Color;



            public static implicit operator ThreadProperty (bool property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (int property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (string property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (float property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (Vector2 property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (Vector3 property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (Quaternion property) => new ThreadProperty(property);

            public static implicit operator ThreadProperty (Color property) => new ThreadProperty(property);
        }
    }
}

public enum UnityMethod
{
    Update,
    LateUpdate,
    FixedUpdate,
    OnGUI,    
}
